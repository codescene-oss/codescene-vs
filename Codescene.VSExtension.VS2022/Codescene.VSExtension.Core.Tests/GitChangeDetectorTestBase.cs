// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    public abstract class GitChangeDetectorTestBase
    {
        protected string _testRepoPath;
        protected GitChangeDetector _detector;
        protected FakeLogger _fakeLogger;
        protected FakeSupportedFileChecker _fakeSupportedFileChecker;
        protected FakeSavedFilesTracker _fakeSavedFilesTracker;
        protected FakeOpenFilesObserver _fakeOpenFilesObserver;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-detector-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);
            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            CommitFile("README.md", "# Test", "Initial commit");

            _fakeLogger = new FakeLogger();
            _fakeSupportedFileChecker = new FakeSupportedFileChecker();
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _detector = new GitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    Directory.Delete(_testRepoPath, true);
                }
                catch
                {
                }
            }
        }

        protected List<string> GetMainBranchCandidates(Repository repo)
        {
            return _detector.GetMainBranchCandidates(repo);
        }

        protected string CommitFile(string filename, string content, string message)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            File.WriteAllText(filePath, content);

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
        }

        protected void ExecGit(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = _testRepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    throw new Exception($"Git command failed: {args}\n{error}");
                }
            }
        }

        protected void CorruptGitObjects()
        {
            var objectsPath = Path.Combine(_testRepoPath, ".git", "objects");
            foreach (var dir in Directory.GetDirectories(objectsPath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Length != 2)
                {
                    continue;
                }

                if (dirName == "pa" || dirName == "in")
                {
                    continue;
                }

                try
                {
                    var files = Directory.GetFiles(dir);
                    if (files.Length > 0)
                    {
                        File.Delete(files[0]);
                        break;
                    }
                }
                catch
                {
                }
            }
        }

        protected void CorruptGitIndex()
        {
            var indexPath = Path.Combine(_testRepoPath, ".git", "index");
            if (File.Exists(indexPath))
            {
                try
                {
                    using (var fs = File.OpenWrite(indexPath))
                    {
                        fs.SetLength(0);
                        fs.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
                    }
                }
                catch
                {
                }
            }
        }

        protected class FakeLogger : ILogger
        {
            public readonly List<string> DebugMessages = new List<string>();
            public readonly List<string> WarnMessages = new List<string>();

            public void Debug(string message) => DebugMessages.Add(message);

            public void Info(string message)
            {
            }

            public void Warn(string message) => WarnMessages.Add(message);

            public void Error(string message, Exception ex)
            {
            }
        }

        protected class FakeSupportedFileChecker : ISupportedFileChecker
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

        protected class FakeSavedFilesTracker : ISavedFilesTracker
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

        protected class FakeOpenFilesObserver : IOpenFilesObserver
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

        protected class TestableGitChangeDetector : GitChangeDetector
        {
            public TestableGitChangeDetector(ILogger logger, ISupportedFileChecker supportedFileChecker)
                : base(logger, supportedFileChecker)
            {
            }

            public bool ThrowInGetChangedFilesFromRepository { get; set; }

            public bool ThrowInGetMergeBaseCommit { get; set; }

            public bool ThrowFromMainBranchCandidates { get; set; }

            public bool ThrowFromFindMergeBase { get; set; }

            public bool ThrowFromDiffCompare { get; set; }

            public bool ThrowFromRetrieveStatus { get; set; }

            public bool SimulateInvalidCurrentBranch { get; set; }

            public bool SimulateInvalidMainBranch { get; set; }

            public string ForceBranchLookupFailure { get; set; }

            public override List<string> GetMainBranchCandidates(Repository repo)
            {
                if (ThrowFromMainBranchCandidates)
                {
                    throw new Exception("Simulated exception from GetMainBranchCandidates");
                }

                var candidates = base.GetMainBranchCandidates(repo);
                if (!string.IsNullOrEmpty(ForceBranchLookupFailure))
                {
                    candidates.Add(ForceBranchLookupFailure);
                }

                return candidates;
            }

            protected override Commit? GetMergeBaseCommit(Repository repo)
            {
                if (SimulateInvalidCurrentBranch)
                {
                    return null;
                }

                if (ThrowInGetMergeBaseCommit)
                {
                    throw new Exception("Simulated exception from GetMergeBaseCommit");
                }

                return base.GetMergeBaseCommit(repo);
            }

            protected override List<string> GetChangedFilesFromRepository(Repository repo, string gitRootPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
            {
                if (ThrowInGetChangedFilesFromRepository)
                {
                    throw new LibGit2SharpException("Simulated LibGit2Sharp exception");
                }

                return base.GetChangedFilesFromRepository(repo, gitRootPath, savedFilesTracker, openFilesObserver);
            }

            protected override Commit? TryFindMergeBase(Repository repo, Branch currentBranch, string candidateName)
            {
                if (SimulateInvalidMainBranch)
                {
                    return null;
                }

                if (ThrowFromFindMergeBase)
                {
                    throw new Exception("Simulated exception from TryFindMergeBase");
                }

                return base.TryFindMergeBase(repo, currentBranch, candidateName);
            }

            protected override List<string> GetCommittedChanges(Repository repo, Commit baseCommit, string gitRootPath)
            {
                if (ThrowFromDiffCompare)
                {
                    throw new Exception("Simulated exception from Diff.Compare");
                }

                return base.GetCommittedChanges(repo, baseCommit, gitRootPath);
            }

            protected override List<string> GetStatusChanges(Repository repo, HashSet<string> filesToExclude, string gitRootPath)
            {
                if (ThrowFromRetrieveStatus)
                {
                    throw new Exception("Simulated exception from RetrieveStatus");
                }

                return base.GetStatusChanges(repo, filesToExclude, gitRootPath);
            }
        }
    }
}
