using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
                CreateNoWindow = true
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

        protected class FakeLogger : ILogger
        {
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message, Exception ex) { }
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
    }
}
