// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RpcJsonNormalizerTests
    {
        [TestMethod]
        public void ToKebabCaseName_ConvertsCamelCaseWithoutSplittingDigits()
        {
            Assert.AreEqual("git-blob-sha", RpcJsonNormalizer.ToKebabCaseName("gitBlobSha"));
            Assert.AreEqual("nippy-b64", RpcJsonNormalizer.ToKebabCaseName("nippyB64"));
            Assert.AreEqual("file-level-code-smells", RpcJsonNormalizer.ToKebabCaseName("fileLevelCodeSmells"));
        }

        [TestMethod]
        public void ToKebabCaseName_LeavesKebabCaseUnchanged()
        {
            Assert.AreEqual("repo-root", RpcJsonNormalizer.ToKebabCaseName("repo-root"));
            Assert.AreEqual("nippy-b64", RpcJsonNormalizer.ToKebabCaseName("nippy-b-64"));
        }

        [TestMethod]
        public void ToKebabCase_NormalizesNestedObjectsAndArrays()
        {
            var input = JObject.Parse(@"{
                ""repoRoot"": ""C:/repo"",
                ""result"": {
                    ""gitBlobSha"": ""abc"",
                    ""fileLevelCodeSmells"": [{ ""highlightRange"": { ""startLine"": 1 } }]
                }
            }");

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(input);

            Assert.AreEqual("C:/repo", (string)normalized["repo-root"]);
            Assert.AreEqual("abc", (string)normalized["result"]["git-blob-sha"]);
            Assert.AreEqual(1, (int)normalized["result"]["file-level-code-smells"][0]["highlight-range"]["start-line"]);
        }
    }
}
