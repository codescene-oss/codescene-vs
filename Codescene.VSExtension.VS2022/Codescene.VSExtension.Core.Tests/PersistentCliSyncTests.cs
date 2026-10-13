// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PersistentCliSyncTests
    {
        [TestMethod]
        public void LogFields_CapsLongListsAndSkipsNulls()
        {
            var fields = LogFields.Format(
                LogFields.Field("path", "src/file.cs"),
                LogFields.Field("missing", null),
                LogFields.Field("files", new[] { "a", "b", "c", "d", "e" }));

            Assert.Contains("path=src/file.cs", fields);
            Assert.DoesNotContain("missing", fields);

            var capped = LogFields.FormatCappedList(new[] { "1", "2", "3" }, 2);
            Assert.AreEqual("1, 2 omitted=1", capped);
        }

        [TestMethod]
        public void DeltaScoreText_ShowsCurrentScoreWhenOldScoreIsMissing()
        {
            var text = DeltaScoreText.Format(new DeltaResponseModel { NewScore = 8.5m });

            Assert.AreEqual(8.5m.ToString(), text);
        }

        [TestMethod]
        public void DeltaScoreText_ShowsBothScoresWhenOldScoreExists()
        {
            var text = DeltaScoreText.Format(new DeltaResponseModel { OldScore = 7m, NewScore = 8.5m });

            Assert.AreEqual(7m + " → " + 8.5m, text);
        }
    }
}
