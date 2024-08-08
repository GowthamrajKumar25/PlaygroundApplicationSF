using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;

namespace documentprocessing_playground.syncfusion
{
    internal class DPLHelper
    {
        internal static void CloneRepository(string formatUrl, string value, HttpRequest req)
        {   
            if (formatUrl != null)
            {
                string url = formatUrl.Replace("%20", " ");

                if (url.Contains(".NET") || url.Contains(".NET-Standard") || url.Contains("NET Standard"))
                {
                    string[] parts = url.Split(',');

                    url = parts[0];
                    int repoIndex = 0;
                    // Splitting the URL and file path
                    if (url.Contains("/master/"))
                    {
                        repoIndex = url.IndexOf("/master/");

                    }
                    else if (url.Contains("/main/"))
                    {
                        repoIndex = url.IndexOf("/main/");
                    }

                    string repoUrl = url.Substring(0, repoIndex);
                    repoUrl = repoUrl.Replace("https://raw.githubusercontent.com", "https://github.com");
                    repoUrl = repoUrl + ".git";
                    string[] listfilePath = url.Substring(repoIndex + 1).Split('/');
                    // Remove the first and last elements
                    string[] finalPath = listfilePath.Skip(1).Take(listfilePath.Length - 2).ToArray();
                    // Join the list elements into a string with '/' as separator
                    string directoryPath = string.Join("/", finalPath);
                    var downloadGit = OnPostDownloadDirectory(repoUrl, directoryPath, value, req);    
                }
                else
                {
                    throw new Exception("Only .NET Core is supported; .NET Framework code is not compatible.");
                }
            }
            
        }
       
        private static async Task<IActionResult> OnPostDownloadDirectory(string repoUrl, string directoryPath, string value, HttpRequest request)
        {
            var user = request.Headers["User-Agent"].ToString();
            var apiUrl = GetGitHubApiUrl(repoUrl, directoryPath);
            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(user);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "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");
            
           
            var extractPath = Path.Combine(Directory.GetCurrentDirectory(), value);

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);

            await DownloadDirectoryContentsAsync(httpClient, apiUrl, extractPath);

            return new PageResult();
        }
        private static async Task DownloadDirectoryContentsAsync(HttpClient httpClient, string apiUrl, string extractPath)
        {
            var response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                return;
            }
            response.EnsureSuccessStatusCode();

            var contents = await response.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<GitHubContent>>(contents);

            if (items == null || items.Count == 0)
            {
                Console.WriteLine("No items found in the directory.");
                return;
            }

            var downloadTasks = new List<Task>();

            foreach (var item in items)
            {
                if (item.type == "file")
                {
                    var filePath = Path.Combine(extractPath, item.name);
                    downloadTasks.Add(DownloadFileAsync(httpClient, item.download_url, filePath));
                }
                else if (item.type == "dir")
                {
                    var dirPath = Path.Combine(extractPath, item.name);
                    Directory.CreateDirectory(dirPath);
                    var dirApiUrl = item.url;
                    downloadTasks.Add(DownloadDirectoryContentsAsync(httpClient, dirApiUrl, dirPath));
                }
            }

            await Task.WhenAll(downloadTasks);
        }
        private static string GetGitHubApiUrl(string repoUrl, string directoryPath)
        {
            var repoParts = repoUrl.Split(new[] { "github.com/", ".git" }, StringSplitOptions.RemoveEmptyEntries);
            if (repoParts.Length < 2)
            {
                throw new ArgumentException("Invalid GitHub repository URL.");
            }

            var repoOwnerAndName = repoParts[1];
            return $"https://api.github.com/repos/{repoOwnerAndName}/contents/{directoryPath}";
        }

        private static async Task DownloadFileAsync(HttpClient httpClient, string fileUrl, string filePath)
        {
            var response = await httpClient.GetAsync(fileUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to download file: {fileUrl}");
                return;
            }
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fs);
        }

        internal static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                System.IO.File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
            }
        }
        internal static string GetBase64Format(string capturedValue)
        {
            string text = "";
            if (capturedValue.EndsWith(".pdf"))
            {
                text = ("data:application/pdf;base64,");
            }
            else if (capturedValue.EndsWith(".docx") || capturedValue.EndsWith(".doc"))
            {
                text = ("data:application/vnd.openxmlformats-officedocument.wordprocessingml.document;base64,");
            }
            else if (capturedValue.EndsWith(".xlsx"))
            {
                text = ("data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,");
            }
            else if (capturedValue.EndsWith(".pptx"))
            {
                text = ("data:application/vnd.openxmlformats-officedocument.presentationml.presentation;base64,");
            }
            return text;

        }
        internal static string SFCompileAndRun(string code, string projectPath)
        {
            if (!string.IsNullOrEmpty(projectPath))
            {
                code = AllClassCodes(projectPath, code);
            }
            
            var originalConsoleOut = Console.Out;
            var output = new StringBuilder();
            // Paths to the NuGet package DLLs
            List<string> referencesSF = new List<string>();

            try
            {

                var syntaxTree = CSharpSyntaxTree.ParseText(code);

                // Get the root compilation unit
                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

                // Add using directives programmatically
                root = root.AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.IO")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Reflection"))
                );

                // Create a new SyntaxTree with the updated root
                syntaxTree = syntaxTree.WithRootAndOptions(root, syntaxTree.Options);

                var references = AppDomain.CurrentDomain.GetAssemblies()
                   .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                   .Select(a => MetadataReference.CreateFromFile(a.Location))
                   .Cast<MetadataReference>();


                List<MetadataReference> metadataReferences = references.ToList() as List<MetadataReference>;
                if (code.Contains("using Syncfusion"))
                {
                    referencesSF = AddSFRequiredAssemblies();
                    foreach (var reference in referencesSF)
                    {
                        metadataReferences.Add(MetadataReference.CreateFromFile(reference));
                    }
                }

                // Create a compilation
                CSharpCompilation compilation = CSharpCompilation.Create(
                    assemblyName: $"DynamicCodeAssembly_{Guid.NewGuid()}",
                    syntaxTrees: new[] { syntaxTree },
                    references: metadataReferences,
                    options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));


                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var failures = result.Diagnostics
                        .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(diagnostic =>
                        {
                            var lineSpan = diagnostic.Location.GetLineSpan();
                            var lineNumber = lineSpan.StartLinePosition.Line + 1;
                            return $"{diagnostic.Id}: {diagnostic.GetMessage()} (Line {lineNumber})";
                        });
                    return $"Compilation Failed:\n{string.Join(Environment.NewLine, failures)}";
                }

                ms.Seek(0, SeekOrigin.Begin);

                var assemblyLoadContext = new AssemblyLoadContext($"DynamicCodeAssemblyContext_{Guid.NewGuid()}", true);
                var assembly = assemblyLoadContext.LoadFromStream(ms);
                var entryPoint = assembly.EntryPoint;

                if (entryPoint == null)
                {
                    return "No entry point found in the code.";
                }

                var parameters = entryPoint.GetParameters();

                using (var sw = new StringWriter(output))
                {
                    Console.SetOut(sw);

                    if (parameters.Length == 0)
                    {
                        entryPoint.Invoke(null, null);
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                    {
                        entryPoint.Invoke(null, new object[] { new string[] { } });
                    }
                    else
                    {
                        return "Unsupported entry point method signature.";
                    }
                }

                Console.SetOut(originalConsoleOut);
                assemblyLoadContext.Unload();

                return output.ToString();
            }
            catch (Exception ex)
            {
                Console.SetOut(originalConsoleOut);
                return $"An error occurred: {ex.InnerException.Message}";
            }
        }
        private static string AllClassCodes(string projectPath, string code)
        {
            // If you want to include files from subdirectories as well, use SearchOption.AllDirectories
            string[] csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);

            if (csFiles.Length > 0)
            {
                var usingStatements = new HashSet<string>();
                var combinedContent = new StringBuilder();

                foreach (string file in csFiles)
                {
                    // Skip Program.cs file
                    if (string.Equals(Path.GetFileName(file), "Program.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string content = System.IO.File.ReadAllText(file);

                    // Extract using statements
                    var matches = Regex.Matches(content, @"^using\s+.*?;\s*$", RegexOptions.Multiline);
                    foreach (Match match in matches)
                    {
                        usingStatements.Add(match.Value);
                    }

                    // Remove using statements from content
                    string contentWithoutUsings = Regex.Replace(content, @"^using\s+.*?;\s*$", string.Empty, RegexOptions.Multiline);
                    combinedContent.AppendLine(contentWithoutUsings.Trim());
                }

                // Combine all using statements and the remaining content
                var finalContent = new StringBuilder();

                foreach (string usingStatement in usingStatements.OrderBy(u => u))
                {
                    finalContent.AppendLine(usingStatement);
                }
                finalContent.AppendLine(code);
                finalContent.AppendLine();
                finalContent.AppendLine(combinedContent.ToString());

                code = finalContent.ToString();
            }
            return code;

        }
        private static List<string> AddSFRequiredAssemblies()
        {
            // Define the assemblies to be copied
            var requiredAssemblies = new List<string>
            {
                "Syncfusion.*.dll",
                "SkiaSharp.dll",
                "SkiaSharp.HarfBuzz.dll",
                "HarfBuzzSharp.dll",
                "Newtonsoft.Json.dll",

            };
            List<string> syncfusionAssemblies = new List<string>();
            foreach (var pattern in requiredAssemblies)
            {
                var assemblies = Directory.GetFiles(AppContext.BaseDirectory, pattern);

                // Load Syncfusion.Net.Core dynamically
                syncfusionAssemblies.AddRange(assemblies);
            }
            return syncfusionAssemblies;
        }

        private class GitHubContent
        {
            public string name { get; set; }
            public string path { get; set; }
            public string sha { get; set; }
            public int size { get; set; }
            public string url { get; set; }
            public string html_url { get; set; }
            public string git_url { get; set; }
            public string download_url { get; set; }
            public string type { get; set; }
        }
    }
}
