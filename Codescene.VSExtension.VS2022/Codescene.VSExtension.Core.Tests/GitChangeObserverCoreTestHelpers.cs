// Copyright (c) CodeScene. All rights reserved.

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
        public TestFileData(string filename, string content, string? commitMessage = null)
        {
            Filename = filename;
            Content = content;
            CommitMessage = commitMessage;
        }

        public string Filename { get; set; }

        public string Content { get; set; }

        public string? CommitMessage { get; set; }
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
            Assert.AreEqual(shouldExist, exists, shouldExist ? $"Should include {filename}" : $"Should not include {filename}");
        }

        public void AssertInTracker(string filePath, bool shouldExist = true)
        {
            var exists = _trackerManager.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
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
        public readonly List<string> DebugMessages = new List<string>();
        public readonly List<string> InfoMessages = new List<string>();
        public readonly List<string> WarnMessages = new List<string>();
        public readonly List<(string, Exception)> ErrorMessages = new List<(string, Exception)>();

        public void Debug(string message)
        {
            DebugMessages.Add(message);
        }

        public void Info(string message, bool statusBar = false)
        {
            InfoMessages.Add(message);
        }

        public void Warn(string message, bool statusBar = false)
        {
            WarnMessages.Add(message);
        }

        public void Error(string message, Exception ex)
        {
            ErrorMessages.Add((message, ex));
        }
    }

    public class FakeCodeReviewer : ICodeReviewer
    {
        public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileReviewModel { FilePath = path, RawScore = "8.5" });
        }

        public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
        {
            var review = await ReviewAsync(path, currentCode, false, cancellationToken);
            var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, string.Empty, cancellationToken);
            return (review, baselineRawScore ?? string.Empty);
        }

        public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default) =>
            Task.FromResult("8.0");

        public FileReviewModel Review(string path, string content) =>
            ReviewAsync(path, content).GetAwaiter().GetResult();

        public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<DeltaResponseModel>(null);

        public DeltaResponseModel Delta(FileReviewModel review, string currentCode) =>
            DeltaAsync(review, currentCode).GetAwaiter().GetResult();
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

        public void ClearSavedFiles()
        {
            _savedFiles.Clear();
        }

        public void RemoveFromTracker(string filePath)
        {
            _savedFiles.Remove(filePath);
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

    public class FakeGitChangeLister : IGitChangeLister
    {
        public event EventHandler<HashSet<string>> FilesDetected;

        public bool ThrowOnCollectFiles { get; set; }

        public HashSet<string> FilesToReturn { get; set; } = new HashSet<string>();

        public Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath)
        {
            return Task.FromResult(new HashSet<string>());
        }

        public Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath)
        {
            return Task.FromResult(new HashSet<string>());
        }

        public void Initialize(string gitRootPath, string workspacePath)
        {
        }

        public void StartPeriodicScanning()
        {
        }

        public void StopPeriodicScanning()
        {
        }

        public Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, string workspacePath)
        {
            if (ThrowOnCollectFiles)
            {
                throw new Exception("Simulated error in CollectFilesFromRepoStateAsync");
            }

            return Task.FromResult(FilesToReturn);
        }

        public void SimulateFilesDetected(HashSet<string> files)
        {
            FilesDetected?.Invoke(this, files);
        }
    }

    internal class TestableGitChangeObserverCore : GitChangeObserverCore
    {
        public TestableGitChangeObserverCore(
            ILogger logger,
            ICodeReviewer codeReviewer,
            ISupportedFileChecker supportedFileChecker,
            IAsyncTaskScheduler taskScheduler,
            IGitChangeLister gitChangeLister,
            IGitService gitService)
            : base(logger, codeReviewer, supportedFileChecker, taskScheduler, gitChangeLister, gitService)
        {
        }

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
