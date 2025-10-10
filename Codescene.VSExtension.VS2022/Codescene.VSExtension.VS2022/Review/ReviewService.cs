
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.CommitBaseline;
using Codescene.VSExtension.VS2022.EditorMargin;
using Codescene.VSExtension.VS2022.TermsAndPolicies;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Codescene.VSExtension.VS2022.UnderlineTagger;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.Review
{
    [Export(typeof(IReviewService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ReviewService : IReviewService
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _reviewer;
        private readonly IErrorListWindowHandler _errorListWindowHandler;
        private readonly CodeSceneMarginSettingsManager _marginSettings;
        private readonly TermsAndPoliciesService _termsAndPoliciesService;
        private CommitBaselineService _commitBaselineService;

        [ImportingConstructor]
        public ReviewService(
            ILogger logger,
            ICodeReviewer reviewer,
            IErrorListWindowHandler errorListWindowHandler,
            CodeSceneMarginSettingsManager marginSettings,
            TermsAndPoliciesService termsAndPoliciesService)
        {
            _logger = logger;
            _reviewer = reviewer;
            _errorListWindowHandler = errorListWindowHandler;
            _marginSettings = marginSettings;
            _termsAndPoliciesService = termsAndPoliciesService;
        }

        /// <summary>
        /// Reviews the content of a file, updates cache and refreshes UI indicators (Code Health margin, error list, tagger).
        /// Triggers delta analysis.
        /// </summary>
        public async Task ReviewContentAsync(string path, ITextBuffer buffer)
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
                {
                    _logger.Info($"File {path} reviewed successfully.");
                    DeltaReviewAsync(result, code).FireAndForget();
                }

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

        /// <summary>
        /// Triggers delta analysis based on review of the most current content in a file.
        /// Updates or opens the Code Health Monitor tool window.
        /// </summary>
        public async Task DeltaReviewAsync(FileReviewModel currentReview, string currentContent)
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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var commitBaselineService = await GetCommitBaselineServiceAsync();
                var baselineType = commitBaselineService.GetCommitBaseline();
                Enum.TryParse(baselineType, true, out CommitBaselineType type);
                var baselineCommitSha = _commitBaselineService.ResolveBaseline(path, type);
                _logger.Info($"Delta analysis using baseline {baselineType} ({baselineCommitSha})");

                DeltaJobTracker.Add(job);

                await CodeSceneToolWindow.UpdateViewAsync(); // Update loading state

                var deltaResult = _reviewer.Delta(currentReview, currentContent, baselineCommitSha);
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

        private async Task<CommitBaselineService> GetCommitBaselineServiceAsync()
        {
            if (_commitBaselineService is null)
            {
                _commitBaselineService = await VS.GetMefServiceAsync<CommitBaselineService>();
            }
            return _commitBaselineService;
        }

        public async Task DeltaReviewOpenDocsAsync()
        {
            var _cache = new ReviewCacheService();
            var _deltaCache = new DeltaCacheService();
            var openDocs = await GetAllOpenEditorPathsAsync();
            var _supportedFileChecker = await VS.GetMefServiceAsync<ISupportedFileChecker>();
            var _reviewService = await VS.GetMefServiceAsync<IReviewService>();


            foreach (var path in _deltaCache.GetAllKeys())
            {
                if (!_supportedFileChecker.IsSupported(path)) return;

                if (!openDocs.Contains(path)) _deltaCache.Remove(path);

                var doc = await VS.Documents.GetDocumentViewAsync(path);
                if (doc is null) continue;

                var buffer = doc.TextBuffer;
                var code = buffer.CurrentSnapshot.GetText();

                var cachedResult = _cache.Get(new ReviewCacheQuery(code, path));
                if (cachedResult is null) continue;

                _logger.Debug($"Re-reviewing {path} due to baseline change...");
                Task.Run(() => _reviewService.DeltaReviewAsync(cachedResult, code)).FireAndForget();
            }
        }

        /// <summary>
        /// Returns full paths of all open editor documents (includes preview tabs).
        /// </summary>
        public static async Task<IReadOnlyList<string>> GetAllOpenEditorPathsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiShell = await VS.GetServiceAsync<SVsUIShell, IVsUIShell>();
            if (uiShell is null)
                return Array.Empty<string>();

            if (!ErrorHandler.Succeeded(uiShell.GetDocumentWindowEnum(out var enumFrames)) || enumFrames is null)
                return Array.Empty<string>();

            var paths = Enumerate(enumFrames)
                .Select(GetDocumentPathOrNull)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .ToList();

            return paths;
        }

        private static IEnumerable<IVsWindowFrame> Enumerate(IEnumWindowFrames frames)
        {
            var arr = new IVsWindowFrame[1];
            while (frames.Next(1, arr, out var fetched) == VSConstants.S_OK && fetched == 1)
            {
                var frame = arr[0];
                if (frame != null)
                    yield return frame;
            }
        }

        private static string GetDocumentPathOrNull(IVsWindowFrame frame)
        {
            return ErrorHandler.Succeeded(
                       frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var mkDoc))
                   && mkDoc is string s
                   && !string.IsNullOrWhiteSpace(s)
                ? s
                : null;
        }
    }
}
