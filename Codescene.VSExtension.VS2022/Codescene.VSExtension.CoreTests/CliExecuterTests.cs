using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Tests
{
    [TestClass]
    public class CliExecuterTests
    {
        private readonly CliExecutor _cliExecuter;
        private readonly CliCommandProvider _cliCommandProvider;

        public CliExecuterTests()
        {
            _cliCommandProvider = new CliCommandProvider();
            _cliExecuter = new CliExecutor(_cliCommandProvider);
        }


        [TestMethod]
        public void Test_Preflight()
        {
            var result = _cliExecuter.Preflight();
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task Test_Refactor()
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
                Assert.IsNotNull(result);
            }
        }

        [TestMethod]
        public async Task Test_Post_Refactor()
        {
            //var path = "C:\\Users\\User\\source\\repos\\codescene-vs\\Codescene.VSExtension.VS2022\\Codescene.VSExtension.CodeSmells\\Issues\\Javascript\\DeepGlobalNestedComplexityExample.js";
            var path = "C:\\Users\\User\\source\\repos\\codescene-vs\\Codescene.VSExtension.VS2022\\Codescene.VSExtension.CodeSmells\\Issues\\CSharp\\DeepGlobalNestedComplexityExample.cs";
            using (var reader = File.OpenText(path))
            {
                string content = reader.ReadToEnd();
                var fileName = Path.GetFileName(path);
                var review = _cliExecuter.ReviewContent(fileName, content);
                var codesmellsJson = JsonConvert.SerializeObject(review.FunctionLevelCodeSmells[0].CodeSmells);
                var preflight = JsonConvert.SerializeObject(_cliExecuter.Preflight());
                var extension = Path.GetExtension(fileName).Replace(".", "");
                var refactorableFunctions = _cliExecuter.FnsToRefactorFromCodeSmells(content, extension, codesmellsJson, preflight);
                var f = refactorableFunctions.First();
                var refactorableFunctionsString = JsonConvert.SerializeObject(f);
                try
                {
                    var refactoredFunctions = _cliExecuter.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: true);
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            Assert.IsTrue(1 == 1);
        }
    }
}
