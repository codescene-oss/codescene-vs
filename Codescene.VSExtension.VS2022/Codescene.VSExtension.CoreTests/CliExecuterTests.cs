using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Codescene.VSExtension.Tests
{
    [TestClass]
    public class CliExecuterTests
    {
        private readonly CliExecuter _cliExecuter;
        private readonly CliCommandProvider _cliCommandProvider;
        private readonly CliSettingsProvider _cliSettingsProvider;

        public CliExecuterTests()
        {
            _cliCommandProvider = new CliCommandProvider();
            _cliSettingsProvider = new CliSettingsProvider();
            _cliExecuter = new CliExecuter(_cliCommandProvider, _cliSettingsProvider);
        }


        [TestMethod]
        public void Test_Preflight()
        {
            var result = _cliExecuter.Preflight();
            Assert.IsNotNull(result);
        }


        [TestMethod]
        public void Test_Refactor()
        {
            var fileName = "DeepGlobalNestedComplexityExample.js";
            var extension = Path.GetExtension(fileName).Replace(".", "");
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\.."));
            var issuesJsDir = Path.Combine(projectRoot, @"Codescene.VSExtension.CodeSmells\Issues\Javascript"); string fullPath = Path.Combine(issuesJsDir, fileName);
            using (var reader = File.OpenText(fullPath))
            {
                string content = reader.ReadToEnd();
                var result = _cliExecuter.ReviewContent(fileName, content);
                var codesmellsJson = JsonConvert.SerializeObject(result.FunctionLevelCodeSmells[0].CodeSmells);
                var preflight = JsonConvert.SerializeObject(_cliExecuter.Preflight());
                var refactor = _cliExecuter.FnsToRefactorFromCodeSmells(content, extension, codesmellsJson, preflight);
                //Assert.IsNotNull(result);
            }
        }
    }
}
