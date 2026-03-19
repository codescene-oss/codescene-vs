// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliObjectScoreCreatorTests
    {
        private Mock<ILogger> _mockLogger;
        private CliObjectScoreCreator _creator;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _creator = new CliObjectScoreCreator(_mockLogger.Object);
        }

        [TestMethod]
        public void Create_BothNull_ReturnsEmpty()
        {
            var result = _creator.Create(null, null);
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Create_BothWhitespace_ReturnsEmpty()
        {
            var result = _creator.Create("  ", "  ");
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Create_SameScores_ReturnsEmptyAndLogsDebug()
        {
            var result = _creator.Create("abc", "abc");
            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("same"))), Times.Once);
        }

        [TestMethod]
        public void Create_OnlyNewScore_ReturnsJsonWithNewScoreOnly()
        {
            var result = _creator.Create(null, "newScore");
            Assert.AreEqual("{\"new-score\":\"newScore\"}", result);
        }

        [TestMethod]
        public void Create_OnlyOldScore_ReturnsJsonWithOldScoreOnly()
        {
            var result = _creator.Create("oldScore", null);
            Assert.AreEqual("{\"old-score\":\"oldScore\"}", result);
        }

        [TestMethod]
        public void Create_BothScores_ReturnsJsonWithBoth()
        {
            var result = _creator.Create("old", "new");
            Assert.AreEqual("{\"old-score\":\"old\",\"new-score\":\"new\"}", result);
        }
    }
}
