// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class UntrackedFileProcessorTests
    {
        private UntrackedFileProcessor _processor;
        private Mock<IGitService> _mockGitService;

        [TestInitialize]
        public void Setup()
        {
            _mockGitService = new Mock<IGitService>();
            _mockGitService.Setup(x => x.IsFileIgnored(It.IsAny<string>())).Returns(false);
            _processor = new UntrackedFileProcessor(_mockGitService.Object);
        }

        [TestMethod]
        public void AddUntrackedFileToDirectory_FileInSubdirectory_AddsToCorrectDirectoryKey()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var relativePath = @"src\Utils\Helper.cs";
            var absolutePath = @"C:\project\src\Utils\Helper.cs";

            _processor.AddUntrackedFileToDirectory(relativePath, absolutePath, untrackedByDirectory);

            Assert.IsTrue(untrackedByDirectory.ContainsKey(@"src\Utils"));
            Assert.HasCount(1, untrackedByDirectory[@"src\Utils"]);
            Assert.AreEqual(absolutePath, untrackedByDirectory[@"src\Utils"][0]);
        }

        [TestMethod]
        public void AddUntrackedFileToDirectory_FileInRoot_UsesRootKey()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var relativePath = "README.md";
            var absolutePath = @"C:\project\README.md";

            _processor.AddUntrackedFileToDirectory(relativePath, absolutePath, untrackedByDirectory);

            Assert.IsTrue(untrackedByDirectory.ContainsKey("__root__"));
            Assert.HasCount(1, untrackedByDirectory["__root__"]);
            Assert.AreEqual(absolutePath, untrackedByDirectory["__root__"][0]);
        }

        [TestMethod]
        public void AddUntrackedFileToDirectory_MultipleFilesSameDirectory_AppendsToExistingList()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var relativePath1 = @"src\File1.cs";
            var absolutePath1 = @"C:\project\src\File1.cs";
            var relativePath2 = @"src\File2.cs";
            var absolutePath2 = @"C:\project\src\File2.cs";

            _processor.AddUntrackedFileToDirectory(relativePath1, absolutePath1, untrackedByDirectory);
            _processor.AddUntrackedFileToDirectory(relativePath2, absolutePath2, untrackedByDirectory);

            Assert.HasCount(1, untrackedByDirectory);
            Assert.IsTrue(untrackedByDirectory.ContainsKey("src"));
            Assert.HasCount(2, untrackedByDirectory["src"]);
            Assert.AreEqual(absolutePath1, untrackedByDirectory["src"][0]);
            Assert.AreEqual(absolutePath2, untrackedByDirectory["src"][1]);
        }

        [TestMethod]
        public void AddUntrackedFileToDirectory_MultipleFilesDifferentDirectories_CreatesSeparateEntries()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var relativePath1 = @"src\File1.cs";
            var absolutePath1 = @"C:\project\src\File1.cs";
            var relativePath2 = @"tests\Test1.cs";
            var absolutePath2 = @"C:\project\tests\Test1.cs";

            _processor.AddUntrackedFileToDirectory(relativePath1, absolutePath1, untrackedByDirectory);
            _processor.AddUntrackedFileToDirectory(relativePath2, absolutePath2, untrackedByDirectory);

            Assert.HasCount(2, untrackedByDirectory);
            Assert.IsTrue(untrackedByDirectory.ContainsKey("src"));
            Assert.IsTrue(untrackedByDirectory.ContainsKey("tests"));
            Assert.HasCount(1, untrackedByDirectory["src"]);
            Assert.HasCount(1, untrackedByDirectory["tests"]);
        }

        [TestMethod]
        public void AddUntrackedFilesToDirectory_IgnoredFile_ShouldNotAdd()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var relativePath = @"src\File1.cs";
            var absolutePath = @"C:\project\src\File1.cs";

            _mockGitService.Setup(x => x.IsFileIgnored(absolutePath)).Returns(true);

            _processor.AddUntrackedFileToDirectory(relativePath, absolutePath, untrackedByDirectory);

            Assert.IsEmpty(untrackedByDirectory);
        }

        [TestMethod]
        public void ProcessUntrackedDirectories_DirectoryWithFiveFiles_AllFilesAdded()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>
            {
                {
                    "src", new List<string>
                    {
                        @"C:\project\src\File1.cs",
                        @"C:\project\src\File2.cs",
                        @"C:\project\src\File3.cs",
                        @"C:\project\src\File4.cs",
                        @"C:\project\src\File5.cs",
                    }
                },
            };
            var savedFiles = new HashSet<string>();
            var changedFiles = new HashSet<string>();

            _processor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);

            Assert.HasCount(5, changedFiles);
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File1.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File5.cs");
        }

        [TestMethod]
        public void ProcessUntrackedDirectories_DirectoryWithSixFiles_OnlySavedFilesAdded()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>
            {
                {
                    "src", new List<string>
                    {
                        @"C:\project\src\File1.cs",
                        @"C:\project\src\File2.cs",
                        @"C:\project\src\File3.cs",
                        @"C:\project\src\File4.cs",
                        @"C:\project\src\File5.cs",
                        @"C:\project\src\File6.cs",
                    }
                },
            };
            var savedFiles = new HashSet<string>
            {
                @"C:\project\src\File2.cs",
                @"C:\project\src\File5.cs",
            };
            var changedFiles = new HashSet<string>();

            _processor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);

            Assert.HasCount(2, changedFiles);
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File2.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File5.cs");
            CollectionAssert.DoesNotContain(changedFiles.ToList(), @"C:\project\src\File1.cs");
        }

        [TestMethod]
        public void ProcessUntrackedDirectories_DirectoryWithSixFilesNoneInSavedFiles_NoFilesAdded()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>
            {
                {
                    "src", new List<string>
                    {
                        @"C:\project\src\File1.cs",
                        @"C:\project\src\File2.cs",
                        @"C:\project\src\File3.cs",
                        @"C:\project\src\File4.cs",
                        @"C:\project\src\File5.cs",
                        @"C:\project\src\File6.cs",
                    }
                },
            };
            var savedFiles = new HashSet<string>();
            var changedFiles = new HashSet<string>();

            _processor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);

            Assert.IsEmpty(changedFiles);
        }

        [TestMethod]
        public void ProcessUntrackedDirectories_MultipleDirectoriesWithMixedCounts_EachProcessedIndependently()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>
            {
                {
                    "src", new List<string>
                    {
                        @"C:\project\src\File1.cs",
                        @"C:\project\src\File2.cs",
                        @"C:\project\src\File3.cs",
                    }
                },
                {
                    "tests", new List<string>
                    {
                        @"C:\project\tests\Test1.cs",
                        @"C:\project\tests\Test2.cs",
                        @"C:\project\tests\Test3.cs",
                        @"C:\project\tests\Test4.cs",
                        @"C:\project\tests\Test5.cs",
                        @"C:\project\tests\Test6.cs",
                    }
                },
                {
                    "__root__", new List<string>
                    {
                        @"C:\project\README.md",
                    }
                },
            };
            var savedFiles = new HashSet<string>
            {
                @"C:\project\tests\Test2.cs",
            };
            var changedFiles = new HashSet<string>();

            _processor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);

            Assert.HasCount(5, changedFiles);
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File1.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File2.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\src\File3.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\tests\Test2.cs");
            CollectionAssert.Contains(changedFiles.ToList(), @"C:\project\README.md");
            CollectionAssert.DoesNotContain(changedFiles.ToList(), @"C:\project\tests\Test1.cs");
        }

        [TestMethod]
        public void ProcessUntrackedDirectories_EmptyDictionary_NoChangesToChangedFiles()
        {
            var untrackedByDirectory = new Dictionary<string, List<string>>();
            var savedFiles = new HashSet<string>();
            var changedFiles = new HashSet<string>();

            _processor.ProcessUntrackedDirectories(untrackedByDirectory, savedFiles, changedFiles);

            Assert.IsEmpty(changedFiles);
        }
    }
}
