using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.VS2022.ErrorList
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType("text")]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class UnderlineTaggerProvider : ITaggerProvider
    {
        [Import]
        private readonly ICodeReviewer _reviewer;

        [Import]
        private readonly ISupportedFileChecker _supportedFileChecker;

        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag
        {
            if (typeof(T) != typeof(IErrorTag))
            {
                return null;
            }

            var path = textBuffer.GetFileName();
            if (SkipShowDiffHelper.PathContainsShowDiffFolder(path))
                return null;

            var linesToUnderline = GetLinesToUnderline(textBuffer);

            var tagger = new UnderlineTagger(textBuffer, linesToUnderline, () => GetRefreshedLinesToUnderline(textBuffer));
            return (ITagger<T>)tagger;
        }

        /// <summary>
        /// Retrieves lines with code smells for the current text buffer.
        /// Uses the saved file on disk as the review source.
        /// </summary>
        /// <param name="textBuffer">The text buffer representing the open document.</param>
        /// <returns>A list of <see cref="CodeSmellModel"/> representing code smells to underline.</returns>
        private List<CodeSmellModel> GetLinesToUnderline(ITextBuffer textBuffer)
        {
            var path = GetPath(textBuffer);
            bool fileNotSupported = isFileNotSupported(path);

            return fileNotSupported ? [] : _reviewer.GetCodesmellExpressions(path);
        }

        /// <summary>
        /// Retrieves updated lines with code smells based on the current (possibly unsaved) content in the text buffer.
        /// Uses the in-memory content instead of the saved file on disk and forces cache invalidation.
        /// </summary>
        /// <param name="textBuffer">The text buffer representing the open document.</param>
        /// <returns>A list of <see cref="CodeSmellModel"/> representing refreshed code smells to underline.</returns>
        private List<CodeSmellModel> GetRefreshedLinesToUnderline(ITextBuffer textBuffer)
        {
            var path = GetPath(textBuffer);
            bool fileNotSupported = isFileNotSupported(path);

            if (fileNotSupported) return [];

            var content = textBuffer.CurrentSnapshot.GetText();
            _reviewer.UseContentOnlyType(content);
            return _reviewer.GetCodesmellExpressions(path, invalidateCache: true);
        }

        private bool isFileNotSupported(string path)
        {
            var extension = Path.GetExtension(path);

            return string.IsNullOrEmpty(path) || _supportedFileChecker.IsNotSupported(extension);
        }

        private string GetPath(ITextBuffer textBuffer)
        {
            string path = null;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                path = textBuffer.GetFileName();
            });

            return path;
        }
    }
}
