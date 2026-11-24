// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentRedactorTests
    {
        [TestMethod]
        public void ScrubsLiteralSecrets()
        {
            var secrets = new[] { "my-secret-token", "another-key-123" };
            var redactor = new AgentRedactor(secrets);

            var input = "Processing with token my-secret-token and key another-key-123";
            var result = redactor.Redact(input);

            Assert.IsFalse(result.Contains("my-secret-token"));
            Assert.IsFalse(result.Contains("another-key-123"));
            Assert.IsTrue(result.Contains("***"));
        }

        [TestMethod]
        public void Redact_AppliesStandardRedactionFirst()
        {
            var secrets = new[] { "literal-secret" };
            var redactor = new AgentRedactor(secrets);

            var input = @"Auth: Bearer sk-123 and literal-secret";
            var result = redactor.Redact(input);

            Assert.IsFalse(result.Contains("sk-123"));
            Assert.IsFalse(result.Contains("literal-secret"));
            Assert.IsTrue(result.Contains("Bearer ***"));
        }

        [TestMethod]
        public void Redact_HandlesNullAndEmptySecrets()
        {
            var redactor1 = new AgentRedactor(null);
            var result1 = redactor1.Redact("some value");
            Assert.AreEqual("some value", result1);

            var redactor2 = new AgentRedactor(new string[] { });
            var result2 = redactor2.Redact("some value");
            Assert.AreEqual("some value", result2);

            var redactor3 = new AgentRedactor(new[] { string.Empty, null, "  " });
            var result3 = redactor3.Redact("some value");
            Assert.AreEqual("some value", result3);
        }
    }
}
