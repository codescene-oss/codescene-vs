using Codescene.VSExtension.VS2022.Review;
using Community.VisualStudio.Toolkit;
using LibGit2Sharp;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    /// <summary>
    /// Monitors a Git repository and triggers:
    ///  - Branch change callback when current branch changes
    ///  - Delta review when the HEAD commit changes
    ///
    /// Implementation detail: we only watch ".git/logs/HEAD".
    /// That file appends on every commit and checkout (any branch, packed refs or not).
    /// </summary>
    public sealed class BranchWatcherService : IDisposable
    {
        private FileSystemWatcher _logsHeadWatcher;

        private string _gitDirPath;
        private string _lastBranch;
        private string _lastCommit;
        private Action<string> _onBranchChanged;

        private DateTime _lastEventTs = DateTime.MinValue;
        private bool _started;

        /// <summary>
        /// Start watching the repo that contains 'solutionPath'. Safe to call repeatedly; subsequent calls reinitialize.
        /// </summary>
        public void StartWatching(string solutionPath, Action<string> onBranchChanged)
        {
            var discovered = Repository.Discover(solutionPath);
            if (string.IsNullOrEmpty(discovered))
            {
                Debug.WriteLine("BranchWatcherService: No git repo found for " + solutionPath);
                return;
            }

            using var repo = new Repository(discovered);
            _gitDirPath = repo.Info.Path;
            _lastBranch = repo.Head?.FriendlyName ?? string.Empty;
            _lastCommit = repo.Head?.Tip?.Sha ?? string.Empty;
            _onBranchChanged = onBranchChanged;

            var logsDir = Path.Combine(_gitDirPath, "logs");
            if (!Directory.Exists(logsDir))
            {
                Debug.WriteLine("BranchWatcherService: '.git/logs' not found. Waiting for first commit.");
                _started = true;
                return;
            }

            _logsHeadWatcher = new FileSystemWatcher(logsDir)
            {
                Filter = "HEAD",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                InternalBufferSize = 64 * 1024
            };

            _logsHeadWatcher.Changed += OnLogsHeadChanged;
            _logsHeadWatcher.Created += OnLogsHeadChanged;
            _logsHeadWatcher.Renamed += OnLogsHeadChanged;
            _logsHeadWatcher.Error += (s, e) =>
                Debug.WriteLine($"BranchWatcherService: FSW error logs/HEAD - {e.GetException()?.Message}");

            _logsHeadWatcher.EnableRaisingEvents = true;
            _started = true;

            Debug.WriteLine($"BranchWatcherService: watching {_gitDirPath}logs\\HEAD");
            Debug.WriteLine($"BranchWatcherService: initial branch={_lastBranch}, tip={_lastCommit}");
        }

        private void OnLogsHeadChanged(object sender, FileSystemEventArgs e)
        {
            if (!ShouldHandleLogsHeadEvent()) return;
            _ = HandleLogsHeadChangedAsync()
                .ContinueWith(t => Debug.WriteLine($"BranchWatcherService: logs/HEAD handler error - {t.Exception?.GetBaseException().Message}"),
                     TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool ShouldHandleLogsHeadEvent()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEventTs).TotalMilliseconds < 250) return false;
            _lastEventTs = now;
            return true;
        }

        private async Task HandleLogsHeadChangedAsync()
        {
            await Task.Delay(60).ConfigureAwait(false);

            if (!TryReadHead(out var currentBranch, out var currentSha))
                return;

            NotifyBranchChangeIfNeeded(currentBranch);
            await NotifyCommitChangeIfNeededAsync(currentSha).ConfigureAwait(false);
        }

        private bool TryReadHead(out string currentBranch, out string currentSha)
        {
            currentBranch = string.Empty;
            currentSha = string.Empty;

            try
            {
                using var repo = new Repository(_gitDirPath);
                currentBranch = repo.Head?.FriendlyName ?? string.Empty;
                currentSha = repo.Head?.Tip?.Sha ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BranchWatcherService: repo read failed - {ex.Message}");
                return false;
            }
        }

        private void NotifyBranchChangeIfNeeded(string currentBranch)
        {
            if (string.Equals(currentBranch, _lastBranch, StringComparison.Ordinal)) return;

            _lastBranch = currentBranch;
            Debug.WriteLine($"BranchWatcherService: Branch changed -> {_lastBranch}");
            _onBranchChanged?.Invoke(_lastBranch);
        }

        private async Task NotifyCommitChangeIfNeededAsync(string currentSha)
        {
            if (string.IsNullOrEmpty(currentSha) ||
                string.Equals(currentSha, _lastCommit, StringComparison.OrdinalIgnoreCase))
                return;

            _lastCommit = currentSha;
            Debug.WriteLine($"BranchWatcherService: New commit -> {_lastCommit}");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var review = await VS.GetMefServiceAsync<IReviewService>();
            if (review is not null)
                await review.DeltaReviewOpenDocsAsync();
        }


        public void Stop()
        {
            _logsHeadWatcher?.Dispose();
            _logsHeadWatcher = null;
            _started = false;
        }

        public void Dispose() => Stop();

        public bool IsRunning => _started;
        public string LastBranch => _lastBranch;
        public string LastCommit => _lastCommit;
        public string GitDirPath => _gitDirPath;
    }
}
