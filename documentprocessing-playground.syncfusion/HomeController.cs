using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Text;

namespace documentprocessing_playground.syncfusion
{
    public class HomeController : Controller
    {
        List<RunCodeResult> resultCol = new List<RunCodeResult>();
        
        private Timer _timer;

        [Microsoft.AspNetCore.Cors.EnableCors("MyPolicy")]
        // GET: HomeController
        public IActionResult Index()
        {
            return View();
        }
        [Microsoft.AspNetCore.Cors.EnableCors("MyPolicy")]
        [HttpPost]
        public async Task<IActionResult> RunCode([FromBody] CodeEditorValue dto)
        {
            var result = await ExecuteCode(dto.Code, dto.GithubRawUrl, dto.GuidText);
            return Ok(result);
        }
       
        private async Task<List<RunCodeResult>> ExecuteCode(string code, string link, string guidPath)
        {
            string output = "";
            List<string> fileBase64 = new List<string>();
            List<string> fileName = new List<string>();
            Dictionary<string, string> fileNameCol = new Dictionary<string, string>();
            if (!code.Contains("using Syncfusion"))
            {
                output = DPLHelper.SFCompileAndRun(code, "");
                var resultVal = new RunCodeResult
                {
                    Output = output,
                    FileBase64 = "",
                    FileName = ""
                };
                resultCol.Add(resultVal);
            }
            else
            {
                string projectPath = Path.GetFullPath(guidPath);
                string sourceFilePath = "";
                if (String.IsNullOrEmpty(link))
                {
                    
                    if (!Directory.Exists(projectPath))
                    {
                        string sourcePath = Path.Combine(Path.GetFullPath("wwwroot"), "Syncfusion.NETCore");
                        Directory.CreateDirectory(projectPath);
                        DPLHelper.CopyDirectory(sourcePath, projectPath);                     
                    }
                    // Path to the source file you want to update
                    sourceFilePath = Path.Combine(projectPath, "Program.cs");
                    code = GetProductLibrary(code, sourceFilePath, out fileName, out fileBase64, out fileNameCol);
                }
                else
                {
                    if (!Directory.Exists(projectPath))
                    {
                        Directory.CreateDirectory(projectPath);
                        
                        DPLHelper.CloneRepository(link, guidPath, Request);
                        Thread.Sleep(1000);
                        
                    } else {
                        
                        // Check if the directory is empty
                        bool isEmpty = IsDirectoryEmpty(projectPath);

                        if (isEmpty)
                        {
                            var resultVal = new RunCodeResult
                            {
                                Output = "Unable to access the code example. To access the same code example again, please refresh the page.",
                                FileBase64 = "",
                                FileName = ""
                            };
                            resultCol.Add(resultVal);
                            return resultCol;
                        }
                    }

                }
                _timer?.Dispose();
                _timer = new Timer(DeleteFolderCallback, projectPath, TimeSpan.FromSeconds(3600), Timeout.InfiniteTimeSpan);

                output = DPLHelper.SFCompileAndRun(RemovePath(code, guidPath), projectPath);

                if (!output.Contains("Compilation Failed"))
                {
                    if (!string.IsNullOrEmpty(link))
                    {
                        string OutputPath = Path.Combine(projectPath, "Output");
                        ReadFilesFromFolder(OutputPath);
                        if (resultCol.Count == 0)
                        {
                            resultCol.Add(new RunCodeResult
                            {
                                Output = "* You must save the documents inside the \"Output\" folder. Otherwise, you can't download the saved documents.",
                                FileBase64 = "",
                                FileName = ""
                            });
                        }
                    }
                    else
                    {
                        if (fileNameCol.Count > 0)
                        {
                            for (int i = 0; i < fileNameCol.Count; i++)
                            {

                                string filePath = System.IO.Path.GetFullPath(fileNameCol[fileName[i]]);
                                if (System.IO.File.Exists(filePath))
                                {
                                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                                    fileBase64[i] = fileBase64[i] + Convert.ToBase64String(new MemoryStream(fileBytes).ToArray());

                                    System.IO.File.Delete(filePath);
                                    resultCol.Add(new RunCodeResult
                                    {
                                        Output = "Completed",
                                        FileBase64 = fileBase64[i],
                                        FileName = Path.GetFileName(fileName[i])
                                    });
                                }
                                else
                                {
                                    resultCol.Add(new RunCodeResult
                                    {
                                        Output = output,
                                        FileBase64 = "",
                                        FileName = ""
                                    });
                                }
                            }

                        }
                        else
                        {
                            var resultVal = new RunCodeResult
                            {
                                Output = output,
                                FileBase64 = "",
                                FileName = ""
                            };
                            resultCol.Add(resultVal);

                        }
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        resultCol.Add(new RunCodeResult
                        {
                            Output = output,
                            FileBase64 = "",
                            FileName = ""
                        });
                    }
                }
                else
                {
                    var resultVal = new RunCodeResult
                    {
                        Output = output,
                        FileBase64 = "",
                        FileName = ""
                    };
                    resultCol.Add(resultVal);
                }
            }
            return resultCol;
        }
      
        private void ReadFilesFromFolder(string folderPath)
        {
            // Get all file paths from the specified directory
            string[] filePaths = Directory.GetFiles(folderPath);

            foreach (string filePath in filePaths)
            {
                if (!filePath.Contains(".gitkeep"))
                {
                    // Get the file name
                    string fileName = Path.GetFileName(filePath);
                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                    string fileBase64 = DPLHelper.GetBase64Format(filePath) + Convert.ToBase64String(new MemoryStream(fileBytes).ToArray());

                    System.IO.File.Delete(filePath);
                    resultCol.Add(new RunCodeResult
                    {
                        Output = "Completed",
                        FileBase64 = fileBase64,
                        FileName = Path.GetFileName(fileName)
                    });
                }
            }
        }
        private string GetProductLibrary(string completeText, string path, out List<string> fileName, out List<string> fileBase64, out Dictionary<string, string> fileNameCol)
        {
            string guidName = "";
            fileBase64 = new List<string>();
            fileName = new List<string>();
            fileNameCol = new Dictionary<string, string>();

            if (completeText.Contains(".Save(") || completeText.Contains(".SaveAs("))
            {
                System.IO.File.WriteAllText(path, completeText);
                // Define the regular expression pattern to match text between double quotes with various file extensions
                string pattern = @"(?<name>[^\""\s]+\.(pdf|pptx|xlsx|doc|docx))\b";
                // Lists to store the matching lines
                List<string> formatLines = new List<string>();
                // Define regular expressions to match lines containing "FileMode.Create" or "File.WriteAllBytes"
                string fileCreatePattern = @"FileMode\.Create";
                string fileWritePattern = @"File\.WriteAllBytes";

                // StringBuilder to store the modified lines
                StringBuilder modifiedContent = new StringBuilder();
                // Read the file line by line
                using (StreamReader reader = new StreamReader(path))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Check if the line contains "FileMode.Create" or "File.WriteAllBytes"
                        if (Regex.IsMatch(line, fileCreatePattern) || Regex.IsMatch(line, fileWritePattern))
                        {
                            MatchCollection matches = Regex.Matches(line, pattern);
                            // Iterate through each match and extract the substring between double quotes
                            foreach (Match match in matches)
                            {
                                // The captured value is in match.Groups[1]
                                string capturedValue = match.Groups[0].Value;

                                // Check the file extension and handle each case accordingly
                                fileBase64.Add(DPLHelper.GetBase64Format(capturedValue));
                                fileName.Add(capturedValue);
                                line = Regex.Replace(line, pattern, m => ReplaceWithGuid(m, out guidName), RegexOptions.IgnoreCase);
                                fileNameCol.Add(capturedValue, guidName);
                            }
                        }
                        modifiedContent.AppendLine(line);
                    }
                }
                System.IO.File.WriteAllText(path, modifiedContent.ToString());

                return modifiedContent.ToString();
            }
            else
            {
                System.IO.File.WriteAllText(path, completeText);

                return completeText;
            }
        }
        private string ReplaceWithGuid(Match match, out string guidName)
        {
            string originalName = match.Groups["name"].Value;
            string extension = Path.GetExtension(originalName);
            guidName = $"{Guid.NewGuid()}{extension}";
            return match.Value.Replace(originalName, guidName);
        }
        
        private string RemovePath(string sourceCode, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var replacements = new Dictionary<string, string>
                {
                    { "../../../", $"{path}/" },
                    { "../../", $"{path}/" },
                    { "../", $"{path}/" },
                    { "\"Output/", $"\"{path}/Output/" },
                    { "@\"Output/", $"@\"{path}/Output/" }
                };

                foreach (var replacement in replacements)
                {
                    sourceCode = sourceCode.Replace(replacement.Key, replacement.Value);
                }
            }

            return sourceCode;

        }
        private void DeleteFolderCallback(object state)
        {
            string folderPath = (string)state;
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
        }
        private bool IsDirectoryEmpty(string path)
        {
            // Check if the directory contains any files or subdirectories
            return Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0;
        }

        #region HelperClass
        public class CodeEditorValue
        {
            public string Code { get; set; }
            public string GithubRawUrl { get; set; }
            public string GuidText { get; set; }
        }

        private class RunCodeResult
        {
            public string Output { get; set; }
            public string FileBase64 { get; set; }
            public string FileName { get; set; }
        }
        #endregion
    }
}