using Codescene.VSExtension.Core.Application.Services.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SupportedFileCheckerTests
    {
        private readonly SupportedFileChecker _checker = new SupportedFileChecker();

        #region Supported Language Extensions

        private static readonly string[] JavaScriptTypeScriptExtensions = { ".js", ".mjs", ".jsx", ".ts", ".tsx", ".vue" };
        private static readonly string[] CFamilyExtensions = { ".c", ".h", ".cc", ".cpp", ".cxx", ".hpp", ".c++", ".m", ".mm" };
        private static readonly string[] DotNetExtensions = { ".cs", ".vb" };
        private static readonly string[] JvmExtensions = { ".java", ".kt", ".groovy", ".scala", ".clj", ".cljc", ".cljs" };
        private static readonly string[] OtherLanguageExtensions = { ".py", ".rb", ".go", ".rs", ".swift", ".php", ".erl", ".dart" };
        private static readonly string[] ScriptingExtensions = { ".pl", ".pm", ".ps1", ".psd1", ".psm1" };
        private static readonly string[] SalesforceExtensions = { ".cls", ".trigger", ".tgr" };

        private static readonly string[] UnsupportedExtensions = { ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".html", ".css", ".sql", ".sh", ".bat", ".exe", ".dll" };

        [TestMethod]
        public void IsSupported_JavaScriptTypeScript_ReturnsTrue()
        {
            AssertExtensionsSupported(JavaScriptTypeScriptExtensions);
        }

        [TestMethod]
        public void IsSupported_CFamily_ReturnsTrue()
        {
            AssertExtensionsSupported(CFamilyExtensions);
        }

        [TestMethod]
        public void IsSupported_DotNet_ReturnsTrue()
        {
            AssertExtensionsSupported(DotNetExtensions);
        }

        [TestMethod]
        public void IsSupported_JvmLanguages_ReturnsTrue()
        {
            AssertExtensionsSupported(JvmExtensions);
        }

        [TestMethod]
        public void IsSupported_OtherLanguages_ReturnsTrue()
        {
            AssertExtensionsSupported(OtherLanguageExtensions);
        }

        [TestMethod]
        public void IsSupported_ScriptingLanguages_ReturnsTrue()
        {
            AssertExtensionsSupported(ScriptingExtensions);
        }

        [TestMethod]
        public void IsSupported_Salesforce_ReturnsTrue()
        {
            AssertExtensionsSupported(SalesforceExtensions);
        }

        [TestMethod]
        public void IsSupported_UnsupportedTypes_ReturnsFalse()
        {
            AssertExtensionsNotSupported(UnsupportedExtensions);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void IsSupported_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(_checker.IsSupported(null));
        }

        [TestMethod]
        public void IsSupported_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(_checker.IsSupported(""));
        }

        [TestMethod]
        public void IsSupported_WhitespacePath_ReturnsFalse()
        {
            Assert.IsFalse(_checker.IsSupported("   "));
        }

        [TestMethod]
        public void IsSupported_NoExtension_ReturnsFalse()
        {
            Assert.IsFalse(_checker.IsSupported("noextension"));
        }

        #endregion

        #region Case Insensitivity

        [TestMethod]
        public void IsSupported_CaseInsensitive_UpperCase_ReturnsTrue()
        {
            AssertAllSupported(new[] { "test.CS", "test.JS", "test.PY" });
        }

        [TestMethod]
        public void IsSupported_CaseInsensitive_MixedCase_ReturnsTrue()
        {
            Assert.IsTrue(_checker.IsSupported("test.Cs"));
        }

        #endregion

        #region Full Paths

        [TestMethod]
        public void IsSupported_WindowsFullPath_ReturnsTrue()
        {
            Assert.IsTrue(_checker.IsSupported("C:\\Projects\\MyApp\\src\\Program.cs"));
        }

        [TestMethod]
        public void IsSupported_UnixFullPath_ReturnsTrue()
        {
            Assert.IsTrue(_checker.IsSupported("/home/user/projects/app/main.py"));
        }

        [TestMethod]
        public void IsSupported_RelativePath_ReturnsExpected()
        {
            Assert.IsTrue(_checker.IsSupported("./relative/path/to/file.java"));
            Assert.IsFalse(_checker.IsSupported("../parent/path/to/file.txt"));
        }

        #endregion

        #region Helper Methods

        private void AssertExtensionsSupported(IEnumerable<string> extensions)
        {
            var unsupportedExtensions = new List<string>();
            foreach (var ext in extensions)
            {
                var fileName = "test" + ext;
                if (!_checker.IsSupported(fileName))
                    unsupportedExtensions.Add(ext);
            }

            Assert.AreEqual(0, unsupportedExtensions.Count,
                $"Expected extensions to be supported: {string.Join(", ", unsupportedExtensions)}");
        }

        private void AssertExtensionsNotSupported(IEnumerable<string> extensions)
        {
            var supportedExtensions = new List<string>();
            foreach (var ext in extensions)
            {
                var fileName = "test" + ext;
                if (_checker.IsSupported(fileName))
                    supportedExtensions.Add(ext);
            }

            Assert.AreEqual(0, supportedExtensions.Count,
                $"Expected extensions NOT to be supported: {string.Join(", ", supportedExtensions)}");
        }

        private void AssertAllSupported(IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
                Assert.IsTrue(_checker.IsSupported(path), $"{path} should be supported");
        }

        #endregion
    }
}
