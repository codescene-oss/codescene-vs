// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DetectedFilePrioritySorterTests
    {
        [TestMethod]
        public void SortByVisibility_PutsVisibleFilesFirst()
        {
            var files = new[] { "c:\\repo\\hidden.cs", "c:\\repo\\visible.cs", "c:\\repo\\other.cs" };
            var visible = new[] { "c:\\repo\\visible.cs" };

            var sorted = DetectedFilePrioritySorter.SortByVisibility(files, visible).ToList();

            Assert.AreEqual("c:\\repo\\visible.cs", sorted[0]);
            Assert.Contains("c:\\repo\\hidden.cs", sorted);
            Assert.Contains("c:\\repo\\other.cs", sorted);
        }

        [TestMethod]
        public void SortByVisibility_PutsActiveDocumentFirstAmongVisible()
        {
            var files = new[] { "c:\\repo\\b.cs", "c:\\repo\\a.cs", "c:\\repo\\hidden.cs" };
            var visible = new[] { "c:\\repo\\a.cs", "c:\\repo\\b.cs" };

            var sorted = DetectedFilePrioritySorter.SortByVisibility(files, visible, "c:\\repo\\b.cs").ToList();

            Assert.AreEqual("c:\\repo\\b.cs", sorted[0]);
            Assert.AreEqual("c:\\repo\\a.cs", sorted[1]);
            Assert.AreEqual("c:\\repo\\hidden.cs", sorted[2]);
        }

        [TestMethod]
        public void SortByVisibility_SortsAlphabeticallyWithinGroups()
        {
            var files = new[] { "c:\\repo\\z.cs", "c:\\repo\\a.cs", "c:\\repo\\m.cs" };

            var sorted = DetectedFilePrioritySorter.SortByVisibility(files, Enumerable.Empty<string>()).ToList();

            Assert.AreEqual("c:\\repo\\a.cs", sorted[0]);
            Assert.AreEqual("c:\\repo\\m.cs", sorted[1]);
            Assert.AreEqual("c:\\repo\\z.cs", sorted[2]);
        }
    }
}
