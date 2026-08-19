// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RpcJsonNormalizerTests
    {
        [TestMethod]
        public void Normalize_ConvertsCamelCaseKeysToKebabCase()
        {
            var input = JObject.Parse("{\"repoRoot\":\"C:/repo\",\"relPath\":\"src/a.cs\"}");

            var normalized = RpcJsonNormalizer.Normalize(input);

            Assert.AreEqual("C:/repo", normalized.Value<string>("repo-root"));
            Assert.AreEqual("src/a.cs", normalized.Value<string>("rel-path"));
        }

        [TestMethod]
        public void Normalize_LeavesKebabCaseKeysUnchanged()
        {
            var input = JObject.Parse("{\"repo-root\":\"C:/repo\",\"git-blob-sha\":\"abc\"}");

            var normalized = RpcJsonNormalizer.Normalize(input);

            Assert.AreEqual("C:/repo", normalized.Value<string>("repo-root"));
            Assert.AreEqual("abc", normalized.Value<string>("git-blob-sha"));
        }
    }
}
