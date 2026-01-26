using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    internal class TestFileData
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

    internal class FileAssertionHelper
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

    internal class FakeLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception ex) { }
    }

    internal class FakeCodeReviewer : ICodeReviewer
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

    internal class FakeSupportedFileChecker : ISupportedFileChecker
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

    internal class FakeGitService : IGitService
    {
        public string GetFileContentForCommit(string path)
        {
            return string.Empty;
        }

        public string GetBranchCreationCommit(string path, Repository repository)
        {
            return string.Empty;
        }
    }

    internal class FakeSavedFilesTracker : ISavedFilesTracker
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

    internal class FakeOpenFilesObserver : IOpenFilesObserver
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

    internal class TestableGitChangeObserver : GitChangeObserver
    {
        public int GetChangedFilesCallCount { get; private set; }

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
