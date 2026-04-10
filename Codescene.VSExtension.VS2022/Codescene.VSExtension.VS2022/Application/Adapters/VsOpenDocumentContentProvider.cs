// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Git;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IOpenDocumentContentProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsOpenDocumentContentProvider : IOpenDocumentContentProvider
    {
        public async Task<string> GetContentForReviewAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var serviceProvider = ServiceProvider.GlobalProvider;
                var frame = TryGetOpenFrame(serviceProvider, filePath);
                return frame != null ? TryGetTextFromFrame(frame) : null;
            });
        }

        private static IVsWindowFrame TryGetOpenFrame(IServiceProvider serviceProvider, string filePath)
        {
            if (serviceProvider == null)
            {
                return null;
            }

            if (VsShellUtilities.IsDocumentOpen(serviceProvider, filePath, Guid.Empty, out _, out _, out var frame))
            {
                return frame;
            }

            var normalizedPath = Path.GetFullPath(filePath);
            if (VsShellUtilities.IsDocumentOpen(serviceProvider, normalizedPath, Guid.Empty, out _, out _, out frame))
            {
                return frame;
            }

            return null;
        }

        private static string TryGetTextFromFrame(IVsWindowFrame frame)
        {
            var textView = VsShellUtilities.GetTextView(frame);
            if (textView == null)
            {
                return null;
            }

            if (textView.GetBuffer(out var lines) != VSConstants.S_OK)
            {
                return null;
            }

            if (lines == null)
            {
                return null;
            }

            if (lines.GetLineCount(out var lineCount) != VSConstants.S_OK)
            {
                return null;
            }

            if (lineCount == 0)
            {
                return string.Empty;
            }

            var lastLineIndex = lineCount - 1;
            if (lines.GetLengthOfLine(lastLineIndex, out var lastLineLength) != VSConstants.S_OK)
            {
                return null;
            }

            if (lines.GetLineText(0, 0, lastLineIndex, lastLineLength, out var text) != VSConstants.S_OK)
            {
                return null;
            }

            return text;
        }
    }
}
