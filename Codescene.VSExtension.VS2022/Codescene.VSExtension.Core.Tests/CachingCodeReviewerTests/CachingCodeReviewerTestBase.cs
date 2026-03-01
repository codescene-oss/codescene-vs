// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;

namespace Codescene.VSExtension.Core.Tests.CachingCodeReviewerTests
{
    public abstract class CachingCodeReviewerTestBase
    {
        protected Mock<ICodeReviewer> _mockInnerReviewer = null!;
        protected Mock<ILogger> _mockLogger = null!;
        protected ReviewCacheService _cacheService = null!;
        protected CachingCodeReviewer _cachingReviewer = null!;

        private string _testId = null!;

        [TestInitialize]
        public void BaseSetup()
        {
            _testId = Guid.NewGuid().ToString("N").Substring(0, 8);

            _mockInnerReviewer = new Mock<ICodeReviewer>();
            _mockLogger = new Mock<ILogger>();
            _cacheService = new ReviewCacheService();
            _cachingReviewer = new CachingCodeReviewer(_mockInnerReviewer.Object, _cacheService, null, null, _mockLogger.Object, null, null);

            Setup();
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            Cleanup();
            _cacheService.Clear();
        }

        protected virtual void Setup()
        {
        }

        protected virtual void Cleanup()
        {
        }

        protected string UniquePath(string baseName)
        {
            return $"{baseName}_{_testId}.cs";
        }

        protected string UniqueContent(string className)
        {
            return $"public class {className}_{_testId} {{ }}";
        }
    }
}
