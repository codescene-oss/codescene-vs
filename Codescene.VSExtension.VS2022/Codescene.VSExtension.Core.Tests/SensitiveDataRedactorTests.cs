// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Reflection;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SensitiveDataRedactorTests
    {
        [TestMethod]
        public void RedactCliTokenArguments_NullOrEmpty_ReturnsInput()
        {
            Assert.IsNull(SensitiveDataRedactor.RedactCliTokenArguments(null));
            Assert.AreEqual(string.Empty, SensitiveDataRedactor.RedactCliTokenArguments(string.Empty));
        }

        [TestMethod]
        public void RedactSecrets_NullOrEmpty_ReturnsInput()
        {
            Assert.IsNull(SensitiveDataRedactor.RedactSecrets(null));
            Assert.AreEqual(string.Empty, SensitiveDataRedactor.RedactSecrets(string.Empty));
        }

        [TestMethod]
        public void RedactPaths_NullOrEmpty_ReturnsInput()
        {
            Assert.IsNull(SensitiveDataRedactor.RedactPaths(null));
            Assert.AreEqual(string.Empty, SensitiveDataRedactor.RedactPaths(string.Empty));
        }

        [TestMethod]
        public void RedactForTelemetry_NullOrEmpty_ReturnsInput()
        {
            Assert.IsNull(SensitiveDataRedactor.RedactForTelemetry(null));
            Assert.AreEqual(string.Empty, SensitiveDataRedactor.RedactForTelemetry(string.Empty));
        }

        [TestMethod]
        public void RedactSecrets_BearerToken_Redacts()
        {
            var result = SensitiveDataRedactor.RedactSecrets("Request failed: Bearer eyJhbGciOiJIUzI1NiJ9");

            Assert.Contains("Bearer ***", result);
            Assert.DoesNotContain("eyJhbGci", result);
        }

        [TestMethod]
        public void RedactSecrets_AuthorizationHeader_Redacts()
        {
            var result = SensitiveDataRedactor.RedactSecrets("Authorization: secret-value");

            Assert.AreEqual("Authorization: ***", result);
        }

        [TestMethod]
        public void RedactSecrets_ApiKeyQueryParam_Redacts()
        {
            var result = SensitiveDataRedactor.RedactSecrets("https://api.test/v1?api_key=secret-key&page=1");

            Assert.Contains("api_key=***", result);
            Assert.DoesNotContain("secret-key", result);
        }

        [TestMethod]
        public void RedactPaths_UncPath_ReplacesWithPlaceholder()
        {
            var result = SensitiveDataRedactor.RedactPaths(@"Error at \\fileserver\share\repo\Main.cs");

            Assert.AreEqual("Error at <path>", result);
        }

        [TestMethod]
        public void RedactPaths_UnixPath_ReplacesWithPlaceholder()
        {
            var result = SensitiveDataRedactor.RedactPaths("Failed in /home/dev/project/src/App.cs");

            Assert.AreEqual("Failed in <path>", result);
        }

        [TestMethod]
        public void RedactPaths_UserProfile_ReplacesWithUserPlaceholder()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(profile))
            {
                Assert.Inconclusive("User profile path is not available on this machine.");
            }

            var result = SensitiveDataRedactor.RedactPaths(profile + "\\Projects\\app\\Main.cs");

            Assert.Contains("<user>", result);
            Assert.DoesNotContain(profile, result, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void RedactPaths_UserProfileAppearsTwice_ReplacesAllOccurrences()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(profile))
            {
                Assert.Inconclusive("User profile path is not available on this machine.");
            }

            var input = "from " + profile + "\\a to " + profile + "\\b";
            var result = SensitiveDataRedactor.RedactPaths(input);

            Assert.DoesNotContain(profile, result, StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(2, CountOccurrences(result, "<user>"));
        }

        [TestMethod]
        public void ReplaceIgnoreCase_EmptyInputs_ReturnsSource()
        {
            var method = GetPrivateStaticMethod("ReplaceIgnoreCase");

            Assert.AreEqual(string.Empty, method.Invoke(null, new object[] { string.Empty, "find", "replace" }));
            Assert.AreEqual("text", method.Invoke(null, new object[] { "text", string.Empty, "replace" }));
        }

        [TestMethod]
        public void NormalizeDirectoryPath_NullOrEmpty_ReturnsEmpty()
        {
            var method = GetPrivateStaticMethod("NormalizeDirectoryPath");

            Assert.AreEqual(string.Empty, method.Invoke(null, new object[] { null }));
            Assert.AreEqual(string.Empty, method.Invoke(null, new object[] { string.Empty }));
            Assert.AreEqual(@"C:\work", method.Invoke(null, new object[] { @"C:\work\" }));
        }

        [TestMethod]
        public void RedactSecrets_MasksApiKeyAndAccessTokenJsonProperties()
        {
            var input1 = @"{""anthropic_api_key"":""sk-ant-1234"",""data"":""value""}";
            var result1 = SensitiveDataRedactor.RedactSecrets(input1);
            Assert.IsFalse(result1.Contains("sk-ant-1234"));
            Assert.IsTrue(result1.Contains(@"""anthropic_api_key"":""***"""));
            Assert.IsTrue(result1.Contains(@"""data"":""value"""));

            var input2 = @"{""codescene_access_token"":""cs_token_abc""}";
            var result2 = SensitiveDataRedactor.RedactSecrets(input2);
            Assert.IsFalse(result2.Contains("cs_token_abc"));
            Assert.IsTrue(result2.Contains(@"""codescene_access_token"":""***"""));

            var input3 = @"{""openai_api_key"":""sk-proj-xyz"",""google_api_key"":""AIza123""}";
            var result3 = SensitiveDataRedactor.RedactSecrets(input3);
            Assert.IsFalse(result3.Contains("sk-proj-xyz"));
            Assert.IsFalse(result3.Contains("AIza123"));
            Assert.IsTrue(result3.Contains(@"""openai_api_key"":""***"""));
            Assert.IsTrue(result3.Contains(@"""google_api_key"":""***"""));

            var input4 = @"{""my_custom_token"":""secret123""}";
            var result4 = SensitiveDataRedactor.RedactSecrets(input4);
            Assert.IsFalse(result4.Contains("secret123"));
            Assert.IsTrue(result4.Contains(@"""my_custom_token"":""***"""));
        }

        private static int CountOccurrences(string source, string substring)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += substring.Length;
            }

            return count;
        }

        private static MethodInfo GetPrivateStaticMethod(string name) =>
            typeof(SensitiveDataRedactor).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Method not found: " + name);
    }
}
