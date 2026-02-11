// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class OpenFilesObserverCoreTests
    {
        private OpenFilesObserverCore _observer;
        private FakeOpenFilesSource _fakeSource;
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public void Setup()
        {
            _fakeSource = new FakeOpenFilesSource();
            _fakeLogger = new FakeLogger();
            _observer = new OpenFilesObserverCore(_fakeSource, _fakeLogger);
        }

        [TestMethod]
        public void GetAllVisibleFileNames_ReturnsEmptyWhenNoFiles()
        {
            var result = _observer.GetAllVisibleFileNames();

            Assert.HasCount(0, result, "Should return empty when no files are open");
        }

        [TestMethod]
        public void GetAllVisibleFileNames_ReturnsOpenFiles()
        {
            _fakeSource.AddPath(@"C:\test\file1.cs");
            _fakeSource.AddPath(@"C:\test\file2.cs");

            var result = _observer.GetAllVisibleFileNames().ToList();

            Assert.HasCount(2, result);
            Assert.Contains(@"C:\test\file1.cs", result);
            Assert.Contains(@"C:\test\file2.cs", result);
        }

        [TestMethod]
        public void GetAllVisibleFileNames_FiltersNullAndEmptyPaths()
        {
            _fakeSource.AddPath(@"C:\test\file1.cs");
            _fakeSource.AddPath(null!);
            _fakeSource.AddPath(string.Empty);
            _fakeSource.AddPath(@"C:\test\file2.cs");

            var result = _observer.GetAllVisibleFileNames().ToList();

            Assert.HasCount(2, result, "Should filter out null and empty paths");
            Assert.Contains(@"C:\test\file1.cs", result);
            Assert.Contains(@"C:\test\file2.cs", result);
        }

        [TestMethod]
        public void GetAllVisibleFileNames_FiltersNonRootedPaths()
        {
            _fakeSource.AddPath(@"C:\test\file1.cs");
            _fakeSource.AddPath(@"relative\path.cs");
            _fakeSource.AddPath(@"C:\test\file2.cs");

            var result = _observer.GetAllVisibleFileNames().ToList();

            Assert.HasCount(2, result, "Should filter out non-rooted paths");
            Assert.Contains(@"C:\test\file1.cs", result);
            Assert.Contains(@"C:\test\file2.cs", result);
        }

        [TestMethod]
        public void GetAllVisibleFileNames_HandlesSourceException()
        {
            _fakeSource.ShouldThrow = true;

            var result = _observer.GetAllVisibleFileNames();

            Assert.HasCount(0, result, "Should return empty when source throws exception");
        }

        [TestMethod]
        public void GetAllVisibleFileNames_HandlesNullFromSource()
        {
            _fakeSource.ReturnNull = true;

            var result = _observer.GetAllVisibleFileNames();

            Assert.HasCount(0, result, "Should return empty when source returns null");
        }

        [TestMethod]
        public void Constructor_ThrowsOnNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenFilesObserverCore(null!, _fakeLogger));
        }

        [TestMethod]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenFilesObserverCore(_fakeSource, null!));
        }
    }

    public class FakeOpenFilesSource : IOpenFilesSource
    {
        private readonly List<string?> _paths = new List<string?>();

        public bool ShouldThrow { get; set; }

        public bool ReturnNull { get; set; }

        public void AddPath(string? path)
        {
            _paths.Add(path);
        }

        public IEnumerable<string>? GetOpenDocumentPaths()
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Test exception");
            }

            if (ReturnNull)
            {
                return null;
            }

            return _paths.Where(p => p != null) !;
        }
    }
}
