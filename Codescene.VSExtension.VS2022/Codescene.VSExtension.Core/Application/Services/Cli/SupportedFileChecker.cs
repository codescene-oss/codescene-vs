using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ISupportedFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SupportedFileChecker : ISupportedFileChecker
    {
        private readonly List<string> _list = new List<string> { ".cs", ".cpp", ".c", ".js", ".ts" };

        public bool IsNotSupported(string extension) => !IsSupported(extension);

        public bool IsSupported(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentNullException(nameof(extension));
            }

            return _list.Contains(extension.ToLower());
        }
    }
}
