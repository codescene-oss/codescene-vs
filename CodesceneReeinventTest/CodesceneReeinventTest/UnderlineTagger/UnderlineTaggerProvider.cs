using Core.Application.Services.FileReviewer;
using Core.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace CodesceneReeinventTest.ErrorList
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(IErrorTag))]
    internal class UnderlineTaggerProvider : ITaggerProvider
    {
        [Import(typeof(IFileReviewer))]
        private readonly IFileReviewer _fileReviewer;
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag
        {
            if (typeof(T) == typeof(IErrorTag))
            {
                var linesToUnderline = GetLinesToUnderline(textBuffer);
                return (ITagger<T>)new UnderlineTagger(textBuffer, linesToUnderline);
            }

            return null;
        }
        private List<ReviewModel> GetLinesToUnderline(ITextBuffer textBuffer)
        {
            string filePath = textBuffer.GetFileName();
            if (filePath == null) return null;
            return _fileReviewer.GetTaggerItems(filePath);
        }
        #region IDocumentEvents methods

        public event EventHandler<DocumentClosedEventArgs> DocumentClosed;

        #endregion IDocumentEvents methods
    }
}
