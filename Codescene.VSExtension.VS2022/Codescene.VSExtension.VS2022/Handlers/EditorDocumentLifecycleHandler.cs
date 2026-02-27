// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.EditorMargin;
using Codescene.VSExtension.VS2022.Tagger;
using Codescene.VSExtension.VS2022.TermsAndPolicies;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.Handlers
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")] // Only call this for code files (e.g .cs, .js)
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
            var filePath = GetFilePath(buffer);
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
            buffer.CurrentSnapshot.GetText();

            // Run on background thread:
            Task.Run(() => ReviewContentAsync(filePath, buffer)).FireAndForget();

            // Triggered when the file content changes (typing, etc.)
            buffer.Changed += (_, _) =>
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
                    TimeSpan.FromSeconds(1));
            };

            textView.Closed += (_, _) =>
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
            {
                return false;
            }

            var lastRefactoring = AceManager.LastRefactoring;
            if (lastRefactoring == null)
            {
                return false;
            }

            // Skip if this file is not the one being refactored
            if (!string.Equals(lastRefactoring.Path, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

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
            {
                return;
            }

            if (!string.Equals(lastRefactoring.Path, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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
                    StartLine = result.UpdatedRange.StartLine,
                    StartColumn = result.UpdatedRange.StartColumn,
                    EndLine = result.UpdatedRange.EndLine,
                    EndColumn = result.UpdatedRange.EndColumn,
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
                var termsAccepted = await _termsAndPoliciesService.EvaluateTermsAndPoliciesAcceptanceAsync();

                if (!termsAccepted)
                {
                    _logger.Warn("Skipping CodeScene analysis, Terms & Policies have not been accepted.", true);
                    return;
                }

                var code = buffer.CurrentSnapshot.GetText();

                var cache = new ReviewCacheService();
                var cachedResult = cache.Get(new ReviewCacheQuery(code, path));
                if (cachedResult != null)
                {
                    return;
                }

                _logger.Info($"Reviewing file {path}...", true);
                var (result, baselineRawScore) = await RunReviewAndBaselineAsync(path, code);
                if (result != null)
                {
                    cache.Put(new ReviewCacheEntry(code, path, result));
                }

                await ApplyReviewResultsAsync(result, code, baselineRawScore, buffer);
            }
            catch (Exception e)
            {
                _logger.Error($"Could not update cache or review file {path}", e);
            }
        }

        private async Task<(FileReviewModel result, string baselineRawScore)> RunReviewAndBaselineAsync(string path, string code)
        {
            var (result, baselineRawScore) = await _reviewer.ReviewAndBaselineAsync(path, code).ConfigureAwait(false);
            return (result, baselineRawScore ?? string.Empty);
        }

        private async Task ApplyReviewResultsAsync(FileReviewModel result, string code, string baselineRawScore, ITextBuffer buffer)
        {
            var path = result?.FilePath ?? string.Empty;
            if (result?.RawScore != null)
            {
                _logger.Info($"File {path} reviewed successfully.");
                await AceUtils.CheckContainsRefactorableFunctionsAsync(result, code);
                await DeltaReviewAsync(result, code, baselineRawScore);
            }
            else
            {
                _logger.Warn($"Review of file {path} returned no results.");
                await CodeSceneToolWindow.UpdateViewAsync();
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _errorListWindowHandler.Handle(result);
            _marginSettings.NotifyScoreUpdated();

            if (buffer.Properties.TryGetProperty<ReviewResultTagger>(typeof(ReviewResultTagger), out var tagger))
            {
                tagger.RefreshTags();
            }
        }

        /// <summary> Try to get the ITextDocument, which contains the file path. </summary>
        private string GetFilePath(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return document.FilePath;
            }
            else
            {
                _logger.Warn("Could not get the file path. Aborting review...");
                return string.Empty;
            }
        }

        /// <summary>
        /// Triggers delta analysis based on review of the most current content in a file.
        /// Updates or opens the Code Health Monitor tool window.
        /// </summary>
        private async Task DeltaReviewAsync(FileReviewModel currentReview, string currentContent, string precomputedBaselineRawScore = null)
        {
            var path = currentReview.FilePath;
            try
            {
                var deltaResult = await _reviewer.DeltaAsync(currentReview, currentContent, precomputedBaselineRawScore);
                await AceUtils.UpdateDeltaCacheWithRefactorableFunctionsAsync(deltaResult, path, currentContent, _logger);
                var scoreChange = deltaResult?.ScoreChange.ToString(CultureInfo.InvariantCulture) ?? "none";
                _logger.Info($"Delta analysis complete for file {path}. Code Health score change: {scoreChange}.");
            }
            catch (Exception e)
            {
                _logger.Error($"Could not perform delta review on file {currentReview.FilePath}.", e);
            }
        }
    }
}
