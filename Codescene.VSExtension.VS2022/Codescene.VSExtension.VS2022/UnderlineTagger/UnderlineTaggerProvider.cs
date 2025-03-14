using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ErrorList
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(IErrorTag))]
    internal class UnderlineTaggerProvider : ITaggerProvider
    {
        [Import(typeof(ICliExecuter))]
        private readonly ICliExecuter _fileReviewer;
        private UnderlineTagger _tagger;
        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag
        {
            if (typeof(T) == typeof(IErrorTag))
            {
                var linesToUnderline = GetLinesToUnderline(textBuffer);
                _tagger = new UnderlineTagger(textBuffer, linesToUnderline, async () => await GetRefreshedLinesToUnderline(textBuffer));
                return (ITagger<T>)_tagger;
            }

            return null;
        }
        private List<ReviewModel> GetLinesToUnderline(ITextBuffer textBuffer)
        {
            string filePath = textBuffer.GetFileName();
            if (filePath == null) return null;
            return _fileReviewer.GetTaggerItems(filePath);
        }
        private async Task<List<ReviewModel>> GetRefreshedLinesToUnderline(ITextBuffer textBuffer)
        {
            OnDocumentChange(textBuffer.GetFileName(), textBuffer.CurrentSnapshot.GetText());
            return _fileReviewer.GetTaggerItems(textBuffer.GetFileName());
        }
        private void OnDocumentChange(string filePath, string content)
        {
            _fileReviewer.AddToActiveReviewList(filePath, content);
        }
    }
}
