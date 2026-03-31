// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CachingGitIgnoreCheckerTests
    {
        private FakeGitIgnoreChecker _fakeChecker;
        private CachingGitIgnoreChecker _cachingChecker;

        [TestInitialize]
        public void Setup()
        {
            _fakeChecker = new FakeGitIgnoreChecker();
            _cachingChecker = new CachingGitIgnoreChecker(_fakeChecker);
        }

        [TestMethod]
        public void IsPathIgnored_SecondCall_UsesCachedValue()
        {
            _fakeChecker.SetResult("file.cs", false);

            _cachingChecker.IsPathIgnored("file.cs");
            _cachingChecker.IsPathIgnored("file.cs");

            Assert.AreEqual(1, _fakeChecker.CallCount, "Second call should use cached value");
        }

        [TestMethod]
        public void IsPathIgnored_CachedFalseValue_ReturnsFalseFromCache()
        {
            _fakeChecker.SetResult("tracked.cs", false);

            var firstResult = _cachingChecker.IsPathIgnored("tracked.cs");
            var cachedResult = _cachingChecker.IsPathIgnored("tracked.cs");

            Assert.IsFalse(firstResult, "First call should return false");
            Assert.IsFalse(cachedResult, "Cached call should also return false");
            Assert.AreEqual(1, _fakeChecker.CallCount, "Should use cache on second call");
        }

        [TestMethod]
        public void IsPathIgnored_DifferentPaths_CallsInnerForEach()
        {
            _fakeChecker.SetResult("file1.cs", false);
            _fakeChecker.SetResult("file2.cs", true);

            _cachingChecker.IsPathIgnored("file1.cs");
            _cachingChecker.IsPathIgnored("file2.cs");

            Assert.AreEqual(2, _fakeChecker.CallCount, "Different paths should each call inner checker");
        }

        [TestMethod]
        public void ClearCache_SubsequentCall_QueriesAgain()
        {
            _fakeChecker.SetResult("file.cs", false);

            _cachingChecker.IsPathIgnored("file.cs");
            _cachingChecker.ClearCache();
            _cachingChecker.IsPathIgnored("file.cs");

            Assert.AreEqual(2, _fakeChecker.CallCount, "After cache clear, should query again");
        }

        [TestMethod]
        public void IsPathIgnored_GitDirectory_ReturnsTrueWithoutCallingInner()
        {
            var result = _cachingChecker.IsPathIgnored(@"C:\repo\.git\config");

            Assert.IsTrue(result, "Files in .git directory should return true");
            Assert.AreEqual(0, _fakeChecker.CallCount, "Should not call inner checker for .git files");
        }

        [TestMethod]
        public void IsPathIgnored_EmptyPath_ReturnsFalse()
        {
            var result = _cachingChecker.IsPathIgnored(string.Empty);

            Assert.IsFalse(result, "Empty path should return false");
            Assert.AreEqual(0, _fakeChecker.CallCount, "Should not call inner checker for empty path");
        }

        [TestMethod]
        public void IsPathIgnored_WhitespacePath_ReturnsFalse()
        {
            var result = _cachingChecker.IsPathIgnored("   ");

            Assert.IsFalse(result, "Whitespace path should return false");
            Assert.AreEqual(0, _fakeChecker.CallCount, "Should not call inner checker for whitespace path");
        }

        [TestMethod]
        public void IsPathIgnored_NullPath_ReturnsFalse()
        {
            var result = _cachingChecker.IsPathIgnored(null);

            Assert.IsFalse(result, "Null path should return false");
            Assert.AreEqual(0, _fakeChecker.CallCount, "Should not call inner checker for null path");
        }

        [TestMethod]
        public void IsPathIgnored_CaseInsensitive_UsesCachedValue()
        {
            _fakeChecker.SetResult("File.cs", false);

            _cachingChecker.IsPathIgnored("File.cs");
            _cachingChecker.IsPathIgnored("file.cs");
            _cachingChecker.IsPathIgnored("FILE.CS");

            Assert.AreEqual(1, _fakeChecker.CallCount, "Cache should be case-insensitive");
        }

        [TestMethod]
        public void IsPathIgnored_ReturnsCorrectResult()
        {
            _fakeChecker.SetResult("ignored.tmp", true);
            _fakeChecker.SetResult("tracked.cs", false);

            var ignoredResult = _cachingChecker.IsPathIgnored("ignored.tmp");
            var trackedResult = _cachingChecker.IsPathIgnored("tracked.cs");

            Assert.IsTrue(ignoredResult, "Should return true for ignored file");
            Assert.IsFalse(trackedResult, "Should return false for tracked file");
        }

        [TestMethod]
        public void CacheCount_AfterMultipleCalls_ReturnsCorrectCount()
        {
            _fakeChecker.SetResult("file1.cs", false);
            _fakeChecker.SetResult("file2.cs", true);
            _fakeChecker.SetResult("file3.cs", false);

            _cachingChecker.IsPathIgnored("file1.cs");
            _cachingChecker.IsPathIgnored("file2.cs");
            _cachingChecker.IsPathIgnored("file3.cs");
            _cachingChecker.IsPathIgnored("file1.cs");

            Assert.AreEqual(3, _cachingChecker.CacheCount, "Cache should contain 3 unique entries");
        }

        [TestMethod]
        public void CacheCount_AfterClearCache_ReturnsZero()
        {
            _fakeChecker.SetResult("file.cs", false);

            _cachingChecker.IsPathIgnored("file.cs");
            Assert.AreEqual(1, _cachingChecker.CacheCount, "Cache should have 1 entry");

            _cachingChecker.ClearCache();
            Assert.AreEqual(0, _cachingChecker.CacheCount, "Cache should be empty after clear");
        }

        [TestMethod]
        public void CacheCount_GitDirectoryPaths_NotCounted()
        {
            _cachingChecker.IsPathIgnored(@"C:\repo\.git\config");
            _cachingChecker.IsPathIgnored(@"C:\repo\.git\HEAD");

            Assert.AreEqual(0, _cachingChecker.CacheCount, ".git directory paths should not be cached");
        }

        private class FakeGitIgnoreChecker : IGitIgnoreChecker
        {
            private Dictionary<string, bool> _results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            public int CallCount { get; private set; }

            public void SetResult(string path, bool ignored)
            {
                _results[path] = ignored;
            }

            public bool IsPathIgnored(string filePath)
            {
                CallCount++;
                return _results.TryGetValue(filePath, out var result) && result;
            }

            public string GetRepositoryRoot(string filePath)
            {
                return null!;
            }
        }
    }
}
