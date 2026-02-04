// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AceRefactorableFunctionsCacheServiceTests
    {
        private AceRefactorableFunctionsCacheService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new AceRefactorableFunctionsCacheService();
            _service.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service.Clear();
        }

        [TestMethod]
        public void Get_WhenCacheMiss_ReturnsEmptyList()
        {
            var query = new AceRefactorableFunctionsQuery("nonexistent.cs", "content");

            var result = _service.Get(query);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Get_WhenCacheHitWithMatchingHash_ReturnsCachedResult()
        {
            var filePath = "test.cs";
            var fileContents = "public class Test {}";
            var cachedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "TestMethod" },
            };

            var entry = new AceRefactorableFunctionsEntry(filePath, fileContents, cachedFunctions);
            _service.Put(entry);

            var query = new AceRefactorableFunctionsQuery(filePath, fileContents);
            var result = _service.Get(query);

            Assert.HasCount(1, result);
            Assert.AreEqual("TestMethod", result[0].Name);
        }

        [TestMethod]
        public void Get_WhenCacheHitWithDifferentHash_ReturnsEmptyList()
        {
            var filePath = "test.cs";
            var originalContents = "public class Test {}";
            var modifiedContents = "public class Test { void Method() {} }";
            var cachedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "TestMethod" },
            };

            var entry = new AceRefactorableFunctionsEntry(filePath, originalContents, cachedFunctions);
            _service.Put(entry);

            var query = new AceRefactorableFunctionsQuery(filePath, modifiedContents);
            var result = _service.Get(query);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Get_WhenCacheHitWithSameContentDifferentPath_ReturnsEmptyList()
        {
            var fileContents = "public class Test {}";
            var cachedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "TestMethod" },
            };

            var entry = new AceRefactorableFunctionsEntry("original.cs", fileContents, cachedFunctions);
            _service.Put(entry);

            var query = new AceRefactorableFunctionsQuery("different.cs", fileContents);
            var result = _service.Get(query);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Get_WhenCacheHitWithMultipleFunctions_ReturnsAllFunctions()
        {
            var filePath = "test.cs";
            var fileContents = "public class Test {}";
            var cachedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "Method1" },
                new FnToRefactorModel { Name = "Method2" },
                new FnToRefactorModel { Name = "Method3" },
            };

            var entry = new AceRefactorableFunctionsEntry(filePath, fileContents, cachedFunctions);
            _service.Put(entry);

            var query = new AceRefactorableFunctionsQuery(filePath, fileContents);
            var result = _service.Get(query);

            Assert.HasCount(3, result);
            Assert.AreEqual("Method1", result[0].Name);
            Assert.AreEqual("Method2", result[1].Name);
            Assert.AreEqual("Method3", result[2].Name);
        }
    }
}
