// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileChangeHandler
    {
        private readonly ILogger _logger;
        private readonly ICodeReviewer _codeReviewer;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly string _workspacePath;
        private readonly TrackerManager _trackerManager;
        private readonly Func<string, string, Task> _onFileReviewedCallback;
        private readonly Action<string> _onFileDeletedCallback;

        public FileChangeHandler(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            string workspacePath,
            TrackerManager trackerManager,
            Func<string, string, Task> onFileReviewedCallback = null,
            Action<string> onFileDeletedCallback = null)
        {
            _logger = logger;
            _codeReviewer = codeReviewer;
            _supportedFileChecker = supportedFileChecker;
            _workspacePath = workspacePath;
            _trackerManager = trackerManager;
            _onFileReviewedCallback = onFileReviewedCallback;
            _onFileDeletedCallback = onFileDeletedCallback;
        }

        public event EventHandler<string> FileDeletedFromGit;

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

            _trackerManager.Add(filePath);

            await ReviewFileAsync(filePath);
        }

        public async Task HandleFileDeleteAsync(string filePath, List<string> changedFiles)
        {
            await Task.Run(() =>
            {
                var wasTracked = _trackerManager.Contains(filePath);
                if (wasTracked)
                {
                    _trackerManager.Remove(filePath);
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

                    var filesToDelete = _trackerManager.GetFilesStartingWith(directoryPrefix);
                    _trackerManager.RemoveAll(filesToDelete);

                    foreach (var fileToDelete in filesToDelete)
                    {
                        FireFileDeletedFromGit(fileToDelete);
                    }
                }
            });
        }

        public bool ShouldProcessFile(string filePath, List<string> changedFiles)
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

        private async Task ReviewFileAsync(string filePath)
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
                    if (_onFileReviewedCallback != null)
                    {
                        await _onFileReviewedCallback(filePath, content);
                    }
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

                FileDeletedFromGit?.Invoke(this, filePath);
                _onFileDeletedCallback?.Invoke(filePath);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error firing file deleted event: {ex.Message}");
            }
        }
    }
}
