using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.ErrorList
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(IErrorTag))]
    public class UnderlineTaggerProvider : ITaggerProvider
    {
        [Import]
        private readonly ICodeReviewer _reviewer;

        private UnderlineTagger _tagger;

        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag
        {
            if (typeof(T) != typeof(IErrorTag))
            {
                return null;
            }

            var linesToUnderline = GetLinesToUnderline(textBuffer);
            _tagger = new UnderlineTagger(textBuffer, linesToUnderline, () => GetRefreshedLinesToUnderline(textBuffer));
            return (ITagger<T>)_tagger;
        }

        private List<CodeSmellModel> GetLinesToUnderline(ITextBuffer textBuffer)
        {
            var path = GetPath(textBuffer);
            return _reviewer.GetCodesmellExpressions(path);
        }
        private List<CodeSmellModel> GetRefreshedLinesToUnderline(ITextBuffer textBuffer)
        {
            var path = GetPath(textBuffer);
            var content = textBuffer.CurrentSnapshot.GetText();
            _reviewer.UseContentOnlyType(content);
            return _reviewer.GetCodesmellExpressions(path);
        }

        private string GetPath(ITextBuffer textBuffer)
        {
            var path = textBuffer.GetFileName();

            if (path == null)
            {
                throw new System.ArgumentNullException(nameof(path));
            }

            return path;
        }
    }
}
