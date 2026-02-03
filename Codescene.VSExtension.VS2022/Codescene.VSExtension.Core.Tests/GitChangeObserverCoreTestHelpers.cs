// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Tests
{
    public class TestFileData
    {
        public string Filename { get; set; }

        public string Content { get; set; }

        public string CommitMessage { get; set; }

        public TestFileData(string filename, string content, string commitMessage = null)
        {
            Filename = filename;
            Content = content;
            CommitMessage = commitMessage;
        }
    }

    public class FileAssertionHelper
    {
        private readonly List<string> _changedFiles;
        private readonly TrackerManager _trackerManager;

        public FileAssertionHelper(List<string> changedFiles, TrackerManager trackerManager)
        {
            _changedFiles = changedFiles;
            _trackerManager = trackerManager;
        }

        public void AssertInChangedList(TestFileData fileData, bool shouldExist = true)
        {
            AssertInChangedList(fileData.Filename, shouldExist);
        }

        public void AssertInChangedList(string filename, bool shouldExist = true)
        {
            var exists = _changedFiles.Any(f => f.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(shouldExist, exists,
                shouldExist ? $"Should include {filename}" : $"Should not include {filename}");
        }

        public void AssertInTracker(string filePath, bool shouldExist = true)
        {
            var exists = _trackerManager.Contains(filePath);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(shouldExist, exists,
                shouldExist ? "File should be in tracker" : "File should not be in tracker");
        }
    }

    public class FakeAsyncTaskScheduler : IAsyncTaskScheduler
    {
        public void Schedule(Func<Task> asyncWork)
        {
            asyncWork().GetAwaiter().GetResult();
        }
    }

    public class FakeLogger : ILogger
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception ex)
        {
        }
    }

    public class FakeCodeReviewer : ICodeReviewer
    {
        public FileReviewModel Review(string path, string content)
        {
            return new FileReviewModel { FilePath = path };
        }

        public DeltaResponseModel Delta(FileReviewModel review, string currentCode)
        {
            return null;
        }
    }

    public class FakeSupportedFileChecker : ISupportedFileChecker
    {
        private readonly Dictionary<string, bool> _supported = new Dictionary<string, bool>();

        public bool IsSupported(string filePath)
        {
            if (_supported.ContainsKey(filePath))
            {
                return _supported[filePath];
            }

            var extension = Path.GetExtension(filePath)?.ToLower();
            return extension == ".ts" || extension == ".js" || extension == ".py" || extension == ".cs";
        }

        public void SetSupported(string filePath, bool isSupported)
        {
            _supported[filePath] = isSupported;
        }
    }

    public class FakeGitService : IGitService
    {
        public string GetFileContentForCommit(string path)
        {
            return string.Empty;
        }

        public bool IsFileIgnored(string filePath)
        {
            return false;
        }

        public string GetBranchCreationCommit(string path, LibGit2Sharp.Repository repository)
        {
            return string.Empty;
        }
    }

    public class FakeSavedFilesTracker : ISavedFilesTracker
    {
        private readonly HashSet<string> _savedFiles = new HashSet<string>();

        public IEnumerable<string> GetSavedFiles()
        {
            return _savedFiles;
        }

        public void AddSavedFile(string filePath)
        {
            _savedFiles.Add(filePath);
        }
    }

    public class FakeOpenFilesObserver : IOpenFilesObserver
    {
        private readonly HashSet<string> _openFiles = new HashSet<string>();

        public IEnumerable<string> GetAllVisibleFileNames()
        {
            return _openFiles;
        }

        public void AddOpenFile(string filePath)
        {
            _openFiles.Add(filePath);
        }
    }

    internal class TestableGitChangeObserverCore : GitChangeObserverCore
    {
        public int GetChangedFilesCallCount { get; private set; }

        public TestableGitChangeObserverCore(ILogger logger, ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker, IGitService gitService,
            IAsyncTaskScheduler taskScheduler)
            : base(logger, codeReviewer, supportedFileChecker, gitService, taskScheduler)
        {
        }

        public void ResetCallCount()
        {
            GetChangedFilesCallCount = 0;
        }

        public override async Task<List<string>> GetChangedFilesVsBaselineAsync()
        {
            GetChangedFilesCallCount++;
            return await base.GetChangedFilesVsBaselineAsync();
        }
    }
}
