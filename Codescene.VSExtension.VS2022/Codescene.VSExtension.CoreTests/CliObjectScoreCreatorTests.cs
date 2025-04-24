using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliObjectScoreCreatorTests
    {
        private readonly ICliObjectScoreCreator _creator = new CliObjectScoreCreator();

        [TestMethod]
        public void Test_Create_Empty_Both()
        {
            const string oldScore = "";
            const string newScore = "";
            var result = _creator.Create(oldScore: oldScore, newScore: newScore);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Test_Create_Equal_Both()
        {
            const string oldScore = "abc";
            const string newScore = "abc";
            var result = _creator.Create(oldScore: oldScore, newScore: newScore);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void Test_Create_Both_Are_Different()
        {
            const string oldScore = "abc";
            const string newScore = "cde";
            var expected = $"{{\"old-score\":\"{oldScore}\",\"new-score\":\"{newScore}\"}}";

            var actual = _creator.Create(oldScore, newScore);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Test_Create_Only_Old_Score()
        {
            const string oldScore = "abc";
            const string newScore = "";
            var expected = $"{{\"old-score\":\"{oldScore}\"}}";

            var actual = _creator.Create(oldScore, newScore);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Test_Create_Only_New_Score()
        {
            const string oldScore = "";
            const string newScore = "cde";
            var expected = $"{{\"new-score\":\"{newScore}\"}}";

            var actual = _creator.Create(oldScore, newScore);

            Assert.AreEqual(expected, actual);
        }
    }
}
