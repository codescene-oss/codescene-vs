// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.EditorMargin;
using Codescene.VSExtension.VS2022.Tagger;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codescene.VSExtension.VS2022.Handlers
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")] // Only call this for code files (e.g .cs, .js)
    [TextViewRole(PredefinedTextViewRoles.Document)] // Only when it's a regular document (not e.g. an output window)
    public class EditorDocumentLifecycleHandler : IWpfTextViewCreationListener
    {
        private static readonly ConditionalWeakTable<ITextBuffer, OpenViewCount> OpenViews = new ConditionalWeakTable<ITextBuffer, OpenViewCount>();

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
        private readonly AceStaleChecker _aceStaleChecker;

        [Import]
        private readonly IGitService _gitService;

        [Import]
        private readonly IAceRefactorService _aceRefactorService;

        [Import]
        private readonly IAsyncTaskScheduler _scheduler;

        [Import]
        private readonly IAceRefactorSuggestedActionsNotifier _aceRefactorSuggestedActionsNotifier;

        [Import]
        private readonly IReviewPipeline _reviewPipeline;

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = textView.TextBuffer;
            var filePath = GetFilePath(buffer);
            var isSupportedForReview = _supportedFileChecker.IsSupported(filePath);

            if (!isSupportedForReview)
            {
                _logger.Debug("[editor] skipped " + LogFields.Format(LogFields.Field("path", filePath), LogFields.Field("reason", "unsupported")));
                return;
            }

            var isIgnored = _gitService.IsFileIgnored(filePath);

            if (isIgnored)
            {
                _logger.Debug("[editor] skipped " + LogFields.Format(LogFields.Field("path", filePath), LogFields.Field("reason", "gitignored")));
                return;
            }

            _logger.Debug($"File opened: {filePath}. ");
            buffer.CurrentSnapshot.GetText();

            var openViews = OpenViews.GetOrCreateValue(buffer);
            openViews.Value++;
            SubscribeToSave(buffer, filePath);
            _scheduler.Schedule(ct => ReviewContentAsync(filePath, buffer));

            buffer.Changed += (_, _) =>
            {
                if (!HasOpenViews(buffer))
                {
                    return;
                }

                if (_gitService.IsFileIgnored(filePath))
                {
                    RemoveGitIgnoredOpenFile(filePath);
                    return;
                }

                if (ShouldCheckAceStaleStatus(filePath))
                {
                    _scheduler.Schedule(async ct =>
                    {
                        await Task.Yield();
                        CheckAndUpdateAceStaleStatus(filePath, buffer);
                    });
                }

                _debounceService.Debounce(
                    filePath,
                    () => _scheduler.Schedule(ct => ReviewContentAsync(filePath, buffer)),
                    TimeSpan.FromSeconds(5));
            };

            textView.Closed += (_, _) =>
            {
                openViews.Value--;
                if (openViews.Value > 0)
                {
                    return;
                }

                _logger.Debug("[editor] closed " + LogFields.Format(LogFields.Field("path", filePath), LogFields.Field("reason", "last-view")));
                OnLastViewClosed(filePath);
            };
        }

        private void SubscribeToSave(ITextBuffer buffer, string filePath)
        {
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return;
            }

            if (buffer.Properties.ContainsProperty(typeof(TextDocumentFileActionEventArgs)))
            {
                return;
            }

            buffer.Properties.AddProperty(typeof(TextDocumentFileActionEventArgs), true);
            document.FileActionOccurred += (_, args) =>
            {
                if (args.FileActionType != FileActionTypes.ContentSavedToDisk || _reviewPipeline == null)
                {
                    return;
                }

                var savedPath = string.IsNullOrEmpty(args.FilePath) ? filePath : args.FilePath;
                var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(savedPath);
                if (string.IsNullOrEmpty(repoRoot))
                {
                    return;
                }

                _reviewPipeline.ReleaseBufferMonitorOwnership(repoRoot, new ReviewDocument { FilePath = savedPath });
            };
        }

        private void OnLastViewClosed(string filePath)
        {
            _debounceService.Cancel(filePath);
            ClearEditorDiagnostics(filePath);
            RestoreOwnedBuffer(filePath);
        }

        private void RemoveGitIgnoredOpenFile(string filePath)
        {
            _debounceService.Cancel(filePath);
            var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(filePath);
            if (string.IsNullOrEmpty(repoRoot) || _reviewPipeline == null)
            {
                return;
            }

            _reviewPipeline.Remove(repoRoot, new ReviewDocument { FilePath = filePath });
        }

        private void RestoreOwnedBuffer(string filePath)
        {
            var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(filePath);
            if (string.IsNullOrEmpty(repoRoot) || _reviewPipeline == null)
            {
                return;
            }

            _reviewPipeline.RestoreFromDiskIfBufferOwned(repoRoot, new ReviewDocument { FilePath = filePath, Content = string.Empty });
        }

        private void ClearEditorDiagnostics(string filePath)
        {
            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler.Handle(new FileReviewModel
                {
                    FilePath = filePath,
                    FileLevel = new System.Collections.Generic.List<CodeSmellModel>(),
                    FunctionLevel = new System.Collections.Generic.List<CodeSmellModel>(),
                });
                _marginSettings.NotifyScoreUpdated();
            });
        }

        private bool HasOpenViews(ITextBuffer buffer)
        {
            return OpenViews.TryGetValue(buffer, out var openViews) && openViews.Value > 0;
        }

        /// <summary>
        /// Quick guard to determine if we should check ACE stale status.
        /// Performs only fast, cheap checks without accessing buffer snapshot.
        /// </summary>
        /// <param name="filePath">The path of the file being edited.</param>
        /// <returns>True if the expensive stale check should be performed.</returns>
        private bool ShouldCheckAceStaleStatus(string filePath)
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
                _scheduler.Schedule(ct => AceToolWindow.MarkAsStaleAsync());
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
            if (!HasOpenViews(buffer))
            {
                return;
            }

            if (_gitService.IsFileIgnored(path))
            {
                RemoveGitIgnoredOpenFile(path);
                return;
            }

            try
            {
                var code = buffer.CurrentSnapshot.GetText();
                var (result, _) = await _reviewer.ReviewWithDeltaAsync(path, code);
                await ApplyReviewResultsAsync(result, buffer);
                await _aceRefactorService.CheckContainsRefactorableFunctionsAsync(result, code);
                _aceRefactorSuggestedActionsNotifier.NotifyAceRefactorableDataChanged(buffer);
                if (!HasOpenViews(buffer))
                {
                    ClearEditorDiagnostics(path);
                    RestoreOwnedBuffer(path);
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Could not update cache or review file {path}", e);
            }
        }

        private async Task ApplyReviewResultsAsync(FileReviewModel result, ITextBuffer buffer)
        {
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

        private sealed class OpenViewCount
        {
            public int Value { get; set; }
        }
    }
}
