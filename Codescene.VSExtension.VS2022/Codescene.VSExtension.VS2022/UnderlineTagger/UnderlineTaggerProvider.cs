using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
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
    public class UnderlineTaggerProvider : ITaggerProvider
    {
        [Import]
        private readonly ICodeReviewer _reviewer;

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
        private List<CodeSmellModel> GetLinesToUnderline(ITextBuffer textBuffer)
        {
            string filePath = textBuffer.GetFileName();
            if (filePath == null) return null;
            return new List<CodeSmellModel>();// _cliExecuter.GetTaggerItems(filePath);
        }
        private async Task<List<CodeSmellModel>> GetRefreshedLinesToUnderline(ITextBuffer textBuffer)
        {
            OnDocumentChange(textBuffer.GetFileName(), textBuffer.CurrentSnapshot.GetText());
            return new List<CodeSmellModel>();// _cliExecuter.GetTaggerItems(textBuffer.GetFileName());
        }
        private void OnDocumentChange(string filePath, string content)
        {
            //_cliExecuter.AddToActiveReviewList(filePath, content);
        }
    }
}
