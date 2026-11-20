// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthRepoPickerTests
    {
        [TestMethod]
        public void Pick_WithoutRoots_IsNull()
        {
            var result = AuthRepoPicker.Pick(null, null);
            Assert.IsNull(result);

            result = AuthRepoPicker.Pick(new string[0], null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Pick_ActiveFileInsideARoot_PicksIt()
        {
            var roots = new[] { "C:\\repos\\project-a", "C:\\repos\\project-b" };
            var activeFile = "C:\\repos\\project-b\\src\\file.cs";

            var result = AuthRepoPicker.Pick(roots, activeFile);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\repos\\project-b", result.Path);
            Assert.AreEqual("project-b", result.Name);
        }

        [TestMethod]
        public void Pick_NestedRoot_PicksTheOutermost()
        {
            var roots = new[] { "C:\\repos\\parent", "C:\\repos\\parent\\nested" };

            var result = AuthRepoPicker.Pick(roots, null);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\repos\\parent", result.Path);
            Assert.AreEqual("parent", result.Name);
        }

        [TestMethod]
        public void Pick_ActiveFileOutsideAllRoots_PicksTheFirstOutermost()
        {
            var roots = new[] { "C:\\repos\\project-a", "C:\\repos\\project-b" };
            var activeFile = "C:\\other\\file.cs";

            var result = AuthRepoPicker.Pick(roots, activeFile);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\repos\\project-a", result.Path);
            Assert.AreEqual("project-a", result.Name);
        }

        [TestMethod]
        public void Pick_WithoutActiveFile_PicksTheFirst()
        {
            var roots = new[] { "C:\\repos\\project-a", "C:\\repos\\project-b" };

            var result = AuthRepoPicker.Pick(roots, null);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\repos\\project-a", result.Path);
            Assert.AreEqual("project-a", result.Name);
        }

        [TestMethod]
        public void Pick_IsCaseAndSeparatorInsensitive()
        {
            var roots = new[] { "c:/repos/PROJECT" };
            var activeFile = "C:\\Repos\\PROJECT\\File.CS";

            var result = AuthRepoPicker.Pick(roots, activeFile);

            Assert.IsNotNull(result);
            Assert.AreEqual("c:/repos/PROJECT", result.Path);
            Assert.AreEqual("PROJECT", result.Name);
        }

        [TestMethod]
        public void Pick_NameIsTheRootFolderName()
        {
            var roots = new[] { "C:\\repos\\my-project\\" };

            var result = AuthRepoPicker.Pick(roots, null);

            Assert.IsNotNull(result);
            Assert.AreEqual("my-project", result.Name);
        }

        [TestMethod]
        public void Pick_WithMultipleNestedRoots_PicksAllOutermostRoots()
        {
            var roots = new[]
            {
                "C:\\repos\\root1",
                "C:\\repos\\root1\\nested1",
                "C:\\repos\\root2",
                "C:\\repos\\root2\\nested2",
            };

            var activeFile = "C:\\repos\\root2\\nested2\\file.cs";

            var result = AuthRepoPicker.Pick(roots, activeFile);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\repos\\root2", result.Path);
        }

        [TestMethod]
        public void Pick_ActiveFileInNestedRepo_PicksTheOutermost()
        {
            var roots = new[] { "C:\\parent", "C:\\parent\\child" };
            var activeFile = "C:\\parent\\child\\file.cs";

            var result = AuthRepoPicker.Pick(roots, activeFile);

            Assert.IsNotNull(result);
            Assert.AreEqual("C:\\parent", result.Path);
        }
    }
}
