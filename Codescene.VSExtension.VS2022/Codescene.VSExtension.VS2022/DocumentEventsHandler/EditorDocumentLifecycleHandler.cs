using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.VS2022.EditorMargin;
using Codescene.VSExtension.VS2022.TermsAndPolicies;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Codescene.VSExtension.VS2022.UnderlineTagger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

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
        private readonly IDebounceService _debounceService;

        [Import]
        private readonly ISupportedFileChecker _supportedFileChecker;

        [Import]
        private readonly CodeSceneMarginSettingsManager _marginSettings;

        [Import]
        private readonly IErrorListWindowHandler _errorListWindowHandler;

        [Import]
        private readonly TermsAndPoliciesService _termsAndPoliciesService;

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = textView.TextBuffer;
            string filePath = GetFilePath(buffer);
            var isSupportedForReview = _supportedFileChecker.IsSupported(filePath);

            if (!isSupportedForReview) return;

            _logger.Debug($"File opened: {filePath}. ");
            string initialContent = buffer.CurrentSnapshot.GetText();

            // Kick off initial review
            ReviewContentAsync(filePath, buffer).FireAndForget();

            // Triggered when the file content changes (typing, etc.)
            buffer.Changed += (sender, args) =>
            {
                _debounceService.Debounce(
                    filePath,
                    () => Task.Run(() => ReviewContentAsync(filePath, buffer)).FireAndForget(),
                    TimeSpan.FromSeconds(3));
            };

            textView.Closed += (sender, args) =>
            {
                _logger.Debug($"File closed: {filePath}...");
                // TODO: Stop any pending analysis for optimization?
            };
        }

        /// <summary>
        /// Reviews the content of a file, updates cache and refreshes UI indicators (Code Health margin, error list, tagger).
        /// </summary>
        private async Task ReviewContentAsync(string path, ITextBuffer buffer)
        {
            try
            {
                var termsAccepted = await _termsAndPoliciesService.EvaulateTermsAndPoliciesAcceptanceAsync();

                if (!termsAccepted)
                {
                    _logger.Warn("Skipping CodeScene analysis, Terms & Policies have not been accepted.");
                    return;
                }

                var code = buffer.CurrentSnapshot.GetText();

                var cache = new ReviewCacheService();
                var cachedResult = cache.Get(new ReviewCacheQuery(code, path));
                if (cachedResult != null) return;

                _logger.Info($"Reviewing file {path}...");
                var result = _reviewer.Review(path, code);

                cache.Put(new ReviewCacheEntry(code, path, result));

                if (result.RawScore != null)
                    _logger.Info($"File {path} reviewed successfully.");

                CodeSceneToolWindow.UpdateViewAsync().FireAndForget();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler.Handle(result);
                _marginSettings.UpdateMarginData(path, code);

                if (buffer.Properties.TryGetProperty<ReviewResultTagger>(typeof(ReviewResultTagger), out var tagger))
                    tagger.RefreshTags();
            }
            catch (Exception e)
            {
                _logger.Error($"Could not update cache or review file {path}", e);
            }
        }

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
    }
}