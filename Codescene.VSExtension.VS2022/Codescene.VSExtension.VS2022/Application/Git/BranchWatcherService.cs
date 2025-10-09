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

        private string _gitDirPath = string.Empty;
        private string _lastBranch = string.Empty;
        private string _lastCommit = string.Empty;
        private Action<string> _onBranchChanged;

        private DateTime _lastEventTs = DateTime.MinValue;
        private bool _started;

        /// <summary>
        /// Start watching the repo that contains 'solutionPath'. Safe to call repeatedly; subsequent calls reinitialize.
        /// </summary>
        public void StartWatching(string solutionPath, Action<string> onBranchChanged)
        {
            Stop(); // clear any existing watcher

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

            // Ensure we can watch logs directory even if HEAD log doesn't exist yet
            var logsDir = Path.Combine(_gitDirPath, "logs");
            if (!Directory.Exists(logsDir))
            {
                // No commits yet -> nothing to watch until the first commit creates logs
                Debug.WriteLine("BranchWatcherService: '.git/logs' not found. Waiting for first commit.");
                _started = true;
                return;
            }

            // Watch ".git/logs" for the "HEAD" file
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

        /// <summary>
        /// Single handler for commits and checkouts via logs/HEAD.
        /// </summary>
        private async void OnLogsHeadChanged(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEventTs).TotalMilliseconds < 250) return;
            _lastEventTs = now;

            try
            {
                await Task.Delay(60);

                string currentBranch;
                string currentSha;

                try
                {
                    using var repo = new Repository(_gitDirPath);
                    currentBranch = repo.Head?.FriendlyName ?? string.Empty;
                    currentSha = repo.Head?.Tip?.Sha ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BranchWatcherService: repo read failed - {ex.Message}");
                    return;
                }

                // Detect branch change
                if (!string.Equals(currentBranch, _lastBranch, StringComparison.Ordinal))
                {
                    _lastBranch = currentBranch;
                    Debug.WriteLine($"BranchWatcherService: Branch changed -> {_lastBranch}");
                    _onBranchChanged?.Invoke(_lastBranch);
                }

                // Detect commit change
                if (!string.IsNullOrEmpty(currentSha) &&
                    !string.Equals(currentSha, _lastCommit, StringComparison.OrdinalIgnoreCase))
                {
                    _lastCommit = currentSha;
                    Debug.WriteLine($"BranchWatcherService: New commit -> {_lastCommit}");

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var review = await VS.GetMefServiceAsync<IReviewService>();
                    if (review is not null)
                        await review.DeltaReviewOpenDocsAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BranchWatcherService: logs/HEAD handler error - {ex.Message}");
            }
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
