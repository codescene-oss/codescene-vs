using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ISupportedFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SupportedFileChecker : ISupportedFileChecker
    {
        private readonly List<string> _supportedExtensions = new List<string>
        {
            ".js", ".mjs", ".sj", ".jsx", ".ts", ".tsx", ".brs", ".bs", ".cls",
            ".tgr", ".trigger", ".c", ".h", ".hh", ".hxx", ".clj", ".cljc", ".cljs", ".cc", ".cpp", ".cxx", ".hpp", ".ipp", ".pcc",  ".c++", ".m", ".mm", ".cs",
            ".erl", ".go", ".groovy", ".java", ".kt", ".php", ".pm", ".pl", ".ps1", ".psd1", ".psm1", ".py", ".rb", ".rs", ".swift", ".vb", ".vue", ".dart", ".scala",
        };

        public bool IsSupported(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var extension = Path.GetExtension(filePath);

            return _supportedExtensions.Contains(extension.ToLower());
        }
    }
}
