using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ISupportedFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SupportedFileChecker : ISupportedFileChecker
    {
        private readonly List<string> _supportedExtensions = new List<string> { ".cs", ".cpp", ".c", ".js", ".ts", ".java" };

        public bool IsSupported(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var extension = Path.GetExtension(filePath);

            return _supportedExtensions.Contains(extension.ToLower());
        }
    }
}
