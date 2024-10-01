using Markdig;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace CodesceneReeinventTest.Application.Handlers
{
    public class MDFileHandler : IMDFileHandler
    {
        public string GetContent(string fileName, string path, string subPath)
        {
            return OpenMarkdownFile(fileName, path, subPath);
        }
        private string OpenMarkdownFile(string fileName, string path, string subPath)
        {
            string toolWindowPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string projectRoot = Directory.GetParent(toolWindowPath).FullName;      
            string mdFilePath = subPath == null ? Path.Combine(Environment.CurrentDirectory, path, fileName + ".md") : Path.Combine(Environment.CurrentDirectory, subPath, path, fileName + ".md");
            if (File.Exists(mdFilePath))
            {
                string markdownContent = File.ReadAllText(mdFilePath, Encoding.UTF8);
                return MDFileContentToHTMLConverter(markdownContent);
            }
            else
            {
                Debug.WriteLine($"Markdown file not found: {mdFilePath}");
                return MDFileContentToHTMLConverter("<p>Markdown file not found!</p>");
            }
        }
        private string MDFileContentToHTMLConverter(string markdownContent)
        {
            return Markdown.ToHtml(markdownContent);
        }
    }
}
