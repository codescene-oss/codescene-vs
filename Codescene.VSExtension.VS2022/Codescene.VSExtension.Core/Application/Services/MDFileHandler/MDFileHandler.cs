﻿using Markdig;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Services.MDFileHandler
{
    [Export(typeof(IMDFileHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MDFileHandler : IMDFileHandler
    {
        private string _fileName = null;
        public string GetContent(string path, string subPath)
        {
            if (_fileName != null)
            {
                return OpenMarkdownFile(path, subPath);
            }

            return null;
        }
        private string OpenMarkdownFile(string path, string subPath)
        {
            string toolWindowPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string projectRoot = Directory.GetParent(toolWindowPath).FullName;
            string mdFilePath = subPath == null ? Path.Combine(toolWindowPath, path, _fileName + ".md") : Path.Combine(toolWindowPath, path, subPath, _fileName + ".md");
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

        public void SetFileName(string fileName)
        {
            _fileName = fileName;
        }
    }
}