using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
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
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.VS2022.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Models.Cli;

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

        [Import]
        private readonly AceStaleChecker _aceStaleChecker;

        [Import]
        private readonly IGitService _gitService;

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = textView.TextBuffer;
            string filePath = GetFilePath(buffer);
            var isSupportedForReview = _supportedFileChecker.IsSupported(filePath);

            if (!isSupportedForReview)
            {
                _logger.Debug($"File '{filePath}' is not supported. Skipping review.");
                return;
            }

            var isIgnored = _gitService.IsFileIgnored(filePath);

            if (isIgnored)
            {
                _logger.Debug($"File '{filePath}' is ignored by Git. Skipping review.");
                return;
            }



            _logger.Debug($"File opened: {filePath}. ");
            string initialContent = buffer.CurrentSnapshot.GetText();

            // Run on background thread:
            Task.Run(() => ReviewContentAsync(filePath, buffer)).FireAndForget();

            // Triggered when the file content changes (typing, etc.)
            buffer.Changed += (sender, args) =>
            {
                // Check ACE staleness - guard first to avoid unnecessary snapshot access
                if (ShouldCheckAceStaleStatus(filePath))
                {
                    // Run the expensive text operations off the UI thread
                    Task.Run(() => CheckAndUpdateAceStaleStatus(filePath, buffer)).FireAndForget();
                }

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
        /// Quick guard to determine if we should check ACE stale status.
        /// Performs only fast, cheap checks without accessing buffer snapshot.
        /// </summary>
        /// <param name="filePath">The path of the file being edited.</param>
        /// <returns>True if the expensive stale check should be performed.</returns>
        private static bool ShouldCheckAceStaleStatus(string filePath)
        {
            // Skip if ACE tool window is not open or already marked as stale
            if (!AceToolWindow.IsCreated() || AceToolWindow.IsStale)
                return false;

            var lastRefactoring = AceManager.LastRefactoring;
            if (lastRefactoring == null)
                return false;

            // Skip if this file is not the one being refactored
            if (!string.Equals(lastRefactoring.Path, filePath, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Checks if the ACE refactoring has become stale due to function changes.
        /// Updates the ACE tool window if staleness is detected.
        /// If the function moved but is unchanged, updates the cached range to the new position.
        /// Note: This method performs expensive text operations and should be called off the UI thread.
        /// </summary>
        private void CheckAndUpdateAceStaleStatus(string filePath, ITextBuffer buffer)
        {
            // Re-check guard conditions in case state changed between scheduling and execution
            var lastRefactoring = AceManager.LastRefactoring;
            if (lastRefactoring == null || AceToolWindow.IsStale)
                return;

            if (!string.Equals(lastRefactoring.Path, filePath, StringComparison.OrdinalIgnoreCase))
                return;

            var content = buffer.CurrentSnapshot.GetText();
            var result = _aceStaleChecker.IsFunctionUnchangedInDocument(
                content,
                lastRefactoring.RefactorableCandidate);

            if (result.IsStale)
            {
                AceToolWindow.MarkAsStaleAsync().FireAndForget();
            }
            else if (result.RangeUpdated && result.UpdatedRange != null)
            {
                // Function moved but content is unchanged - update the cached range atomically
                // Create a new Range instance for atomic assignment to avoid readers seeing partially-updated state
                var newRange = new CliRangeModel
                {
                    Startline = result.UpdatedRange.Startline,
                    StartColumn = result.UpdatedRange.StartColumn,
                    EndLine = result.UpdatedRange.EndLine,
                    EndColumn = result.UpdatedRange.EndColumn
                };
                lastRefactoring.RefactorableCandidate.Range = newRange;
            }
        }

        /// <summary>
        /// Reviews the content of a file, updates cache and refreshes UI indicators (Code Health margin, error list, tagger).
        /// Triggers delta analysis.
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

                if (result != null && result.RawScore != null)
                {
                    _logger.Info($"File {path} reviewed successfully.");
                    // this call has to be awaited, otherwise delta could finish before and update of delta cache won't work
                    // happening in 17.0.0
                    await AceUtils.CheckContainsRefactorableFunctionsAsync(result, code);

                    DeltaReviewAsync(result, code).FireAndForget();
                }
                else
                {
                    _logger.Warn($"Review of file {path} returned no results.");
                }

                await CodeSceneToolWindow.UpdateViewAsync();

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

        /// <summary>
        /// Triggers delta analysis based on review of the most current content in a file.
        /// Updates or opens the Code Health Monitor tool window.
        /// </summary>
        private async Task DeltaReviewAsync(FileReviewModel currentReview, string currentContent)
        {
            var path = currentReview.FilePath;
            var job = new Job
            {
                Type = JobTypes.DELTA,
                State = StateTypes.RUNNING,
                File = new File { FileName = path }
            };

            try
            {
                DeltaJobTracker.Add(job);

                await CodeSceneToolWindow.UpdateViewAsync(); // Update loading state

                var deltaResult = _reviewer.Delta(currentReview, currentContent);
                await AceUtils.UpdateDeltaCacheWithRefactorableFunctions(deltaResult, path, currentContent, _logger);

                var scoreChange = deltaResult?.ScoreChange.ToString() ?? "none";
                _logger.Info($"Delta analysis complete for file {path}. Code Health score change: {scoreChange}.");
            }
            catch (Exception e)
            {
                _logger.Error($"Could not perform delta review on file {currentReview.FilePath}.", e);
            }
            finally
            {
                DeltaJobTracker.Remove(job);
                await CodeSceneToolWindow.UpdateViewAsync();
            }
        }
    }
}
