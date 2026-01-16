using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class SupportedFileCheckerTests
    {
        private readonly SupportedFileChecker _checker;

        public SupportedFileCheckerTests()
        {
            _checker = new SupportedFileChecker();
        }

        // JavaScript/TypeScript
        [DataTestMethod]
        [DataRow("test.js", true)]
        [DataRow("test.mjs", true)]
        [DataRow("test.jsx", true)]
        [DataRow("test.ts", true)]
        [DataRow("test.tsx", true)]
        [DataRow("test.vue", true)]
        public void IsSupported_JavaScriptTypeScript_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // C-family languages
        [DataTestMethod]
        [DataRow("test.c", true)]
        [DataRow("test.h", true)]
        [DataRow("test.cc", true)]
        [DataRow("test.cpp", true)]
        [DataRow("test.cxx", true)]
        [DataRow("test.hpp", true)]
        [DataRow("test.c++", true)]
        [DataRow("test.m", true)]
        [DataRow("test.mm", true)]
        public void IsSupported_CFamily_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // .NET languages
        [DataTestMethod]
        [DataRow("test.cs", true)]
        [DataRow("test.vb", true)]
        public void IsSupported_DotNet_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // JVM languages
        [DataTestMethod]
        [DataRow("test.java", true)]
        [DataRow("test.kt", true)]
        [DataRow("test.groovy", true)]
        [DataRow("test.scala", true)]
        [DataRow("test.clj", true)]
        [DataRow("test.cljc", true)]
        [DataRow("test.cljs", true)]
        public void IsSupported_JvmLanguages_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Other languages
        [DataTestMethod]
        [DataRow("test.py", true)]
        [DataRow("test.rb", true)]
        [DataRow("test.go", true)]
        [DataRow("test.rs", true)]
        [DataRow("test.swift", true)]
        [DataRow("test.php", true)]
        [DataRow("test.erl", true)]
        [DataRow("test.dart", true)]
        public void IsSupported_OtherLanguages_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Scripting languages
        [DataTestMethod]
        [DataRow("test.pl", true)]
        [DataRow("test.pm", true)]
        [DataRow("test.ps1", true)]
        [DataRow("test.psd1", true)]
        [DataRow("test.psm1", true)]
        public void IsSupported_ScriptingLanguages_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Salesforce
        [DataTestMethod]
        [DataRow("test.cls", true)]
        [DataRow("test.trigger", true)]
        [DataRow("test.tgr", true)]
        public void IsSupported_Salesforce_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Unsupported file types
        [DataTestMethod]
        [DataRow("test.txt", false)]
        [DataRow("test.md", false)]
        [DataRow("test.json", false)]
        [DataRow("test.xml", false)]
        [DataRow("test.yaml", false)]
        [DataRow("test.yml", false)]
        [DataRow("test.html", false)]
        [DataRow("test.css", false)]
        [DataRow("test.sql", false)]
        [DataRow("test.sh", false)]
        [DataRow("test.bat", false)]
        [DataRow("test.exe", false)]
        [DataRow("test.dll", false)]
        public void IsSupported_UnsupportedTypes_ReturnsFalse(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Edge cases
        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow("   ", false)]
        [DataRow("noextension", false)]
        public void IsSupported_EdgeCases_ReturnsFalse(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Case insensitivity
        [DataTestMethod]
        [DataRow("test.CS", true)]
        [DataRow("test.Cs", true)]
        [DataRow("test.JS", true)]
        [DataRow("test.PY", true)]
        public void IsSupported_CaseInsensitive_ReturnsTrue(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }

        // Full paths
        [DataTestMethod]
        [DataRow("C:\\Projects\\MyApp\\src\\Program.cs", true)]
        [DataRow("/home/user/projects/app/main.py", true)]
        [DataRow("./relative/path/to/file.java", true)]
        [DataRow("../parent/path/to/file.txt", false)]
        public void IsSupported_FullPaths_ReturnsExpected(string filePath, bool expected)
        {
            Assert.AreEqual(expected, _checker.IsSupported(filePath));
        }
    }
}
