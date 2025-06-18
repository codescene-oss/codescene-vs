using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.Util;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")] // Only call this for code files (e.g. .cs, .js)
    [TextViewRole(PredefinedTextViewRoles.Document)] // Only when it's a regular document (not e.g. an output window)
    public class EditorDocumentLifecycleHandler : IWpfTextViewCreationListener
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICodeReviewer _reviewer;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly IDebounceService _debounceService;

        /*
         * Called when the editor opens a code file.
           From here you can hook into events like editing or closing.
         * On file open:
            Get path
            Get snapshot text
            Trigger analysis
         * On file change(via buffer.Changed):
            Debounce the calls
            On pause in typing:
            Get path
            Get latest snapshot text
            Trigger analysis
        */
        public void TextViewCreated(IWpfTextView textView)
        {
            ITextBuffer buffer = textView.TextBuffer;
            string filePath = GetFilePath(buffer);
            if (filePath.Equals("")) return;

            _logger.Info($"File opened: {filePath}. Performing initial review...");
            string initialContent = buffer.CurrentSnapshot.GetText();

            ReviewContent(filePath, initialContent);

            // Triggered when the file content changes (typing, etc.)
            buffer.Changed += (sender, args) =>
            {
                _logger.Info($"File edited: {filePath}...");
                string currentContent = buffer.CurrentSnapshot.GetText();

                _debounceService.Debounce(
                    filePath,
                    () => ReviewContent(filePath, currentContent),
                    TimeSpan.FromSeconds(2));
            };

            textView.Closed += (sender, args) =>
            {
                _logger.Info($"File closed: {filePath}...");
                // TODO: Stop any pending analysis
            };
        }

        // TODO: move to helper?
        ///<summary> Try to get the ITextDocument, which contains the file path </summary>
        private string GetFilePath(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                return document.FilePath;
            else
            {
                _logger.Warn("Could not get the file path. Aborting review...");
                return "";
            }
        }

        private void ReviewContent(string path, string code)
        {
            try
            {
                _logger.Info("Reviewing [ReviewContent]");
                var cache = new ReviewCacheService();
                var cachedResult = cache.Get(new ReviewCacheQuery(code, path));
                if (cachedResult != null) return;

                var result = _reviewer.ReviewFileContent(path, code);
                var mapped = _mapper.Map(path, result);
                _logger.Info($"File was not in cache. Finished reviewing file {path} from [TextViewCreated]. Updating cache....");

                cache.Put(new ReviewCacheEntry(code, path, mapped));

                // TODO:
                _logger.Info("Updating the tagger...");
                ReviewCacheEvents.OnCacheUpdated(path);
            }
            catch (Exception e)
            {
                _logger.Error($"Could not update cache or review file {path}", e);
            }
        }

    }
}
