using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    internal class FileChangeHandler
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly string _workspacePath;
        private readonly HashSet<string> _tracker;
        private readonly object _trackerLock;

        public event EventHandler<FileDeletedEventArgs> FileDeletedFromGit;

        public FileChangeHandler(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            string workspacePath,
            HashSet<string> tracker,
            object trackerLock)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _workspacePath = workspacePath;
            _tracker = tracker;
            _trackerLock = trackerLock;
        }

        public async Task HandleFileChangeAsync(string filePath, List<string> changedFiles)
        {
            var isDirectory = !Path.HasExtension(filePath);
            if (isDirectory)
            {
                return;
            }

            if (!ShouldProcessFile(filePath, changedFiles))
            {
                return;
            }

            lock (_trackerLock)
            {
                _tracker.Add(filePath);
            }

            await Task.Run(() => ReviewFile(filePath));
        }

        public async Task HandleFileDeleteAsync(string filePath, List<string> changedFiles)
        {
            await Task.Run(() =>
            {
                bool wasTracked;
                lock (_trackerLock)
                {
                    wasTracked = _tracker.Contains(filePath);
                    if (wasTracked)
                    {
                        _tracker.Remove(filePath);
                    }
                }

                if (wasTracked)
                {
                    FireFileDeletedFromGit(filePath);
                    return;
                }

                if (ShouldProcessFile(filePath, changedFiles))
                {
                    FireFileDeletedFromGit(filePath);
                    return;
                }

                var isDirectory = !Path.HasExtension(filePath);
                if (isDirectory)
                {
                    var directoryPrefix = filePath.EndsWith(Path.DirectorySeparatorChar.ToString())
                        ? filePath
                        : filePath + Path.DirectorySeparatorChar;

                    List<string> filesToDelete;
                    lock (_trackerLock)
                    {
                        filesToDelete = _tracker.Where(tf => tf.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

                        foreach (var fileToDelete in filesToDelete)
                        {
                            _tracker.Remove(fileToDelete);
                        }
                    }

                    foreach (var fileToDelete in filesToDelete)
                    {
                        FireFileDeletedFromGit(fileToDelete);
                    }
                }
            });
        }

        private bool ShouldProcessFile(string filePath, List<string> changedFiles)
        {
            if (!_supportedFileChecker.IsSupported(filePath))
            {
                return false;
            }

            if (!IsFileInChangedList(filePath, changedFiles))
            {
                return false;
            }

            return true;
        }

        private bool IsFileInChangedList(string filePath, List<string> changedFiles)
        {
            if (string.IsNullOrEmpty(_workspacePath))
            {
                return true;
            }

            var relativePath = PathUtilities.GetRelativePath(_workspacePath, filePath);

            var normalizedRelativePath = relativePath.Replace('\\', '/');

            return changedFiles.Any(cf => cf.Replace('\\', '/').Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase));
        }

        private void ReviewFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                var content = File.ReadAllText(filePath);
                var review = _codeReviewer.Review(filePath, content);

                if (review != null)
                {
                    _logger?.Debug($"GitChangeObserver: File reviewed: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Could not load file for review {filePath}: {ex.Message}");
            }
        }

        private void FireFileDeletedFromGit(string filePath)
        {
            try
            {
                _logger?.Debug($"GitChangeObserver: File deleted from git: {filePath}");

                FileDeletedFromGit?.Invoke(this, new FileDeletedEventArgs(filePath));
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error firing file deleted event: {ex.Message}");
            }
        }
    }
}
