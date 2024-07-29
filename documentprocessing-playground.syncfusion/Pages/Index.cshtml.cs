using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace documentprocessing_playground.syncfusion.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string Url { get; set; }
        public async Task OnGet()
        {
            var value = Guid.NewGuid().ToString();
            ViewData["PostGuidResult"] = value;
            Url = null;
            string code = "using System;\r\nnamespace HelloWorld\r\n{\r\n   public class Program\r\n   {\r\n       public static void Main()\r\n       {\r\n            Console.WriteLine(\"Hello, World!\");\r\n       }\r\n   }\r\n}";
            DPLHelper.SFCompileAndRun(code, "");
        }

        public void OnPost()
        {
            var value = Guid.NewGuid().ToString();
            // Handle the posted data
            ViewData["PostResult"] = Url;
            ViewData["PostGuidResult"] = value;
            DPLHelper.CloneRepository(Url, value, Request);
            string code = "using Syncfusion;\r\nusing System;\r\nnamespace HelloWorld\r\n{\r\n   public class Program\r\n   {\r\n       public static void Main()\r\n       {\r\n            Console.WriteLine(\"Hello, World!\");\r\n       }\r\n   }\r\n}";
            DPLHelper.SFCompileAndRun(code, "");

        }
    }
}
