using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.IntegrationTests
{
    /// <summary>
    /// Integration tests that require the actual CodeScene CLI to be installed and available.
    /// These tests interact with the real CLI executable and are not suitable for CI/CD pipelines
    /// unless the CLI is properly configured.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CliExecutorIntegrationTests
    {
        [TestMethod]
        public void Test()
        {
        }
        // NOTE: These tests require the actual CLI to be installed.
        // They are currently disabled until the CliExecutor dependencies are properly configured.
        // The CliExecutor now requires multiple dependencies via [ImportingConstructor].

        //private readonly CliExecutor _cliExecutor;
        //private readonly CliCommandProvider _cliCommandProvider;

        //public CliExecutorIntegrationTests()
        //{
        //    // TODO: Set up MEF container or manually create all required dependencies
        //    // _cliCommandProvider = new CliCommandProvider();
        //    // _cliExecutor = new CliExecutor(...);
        //}

        //[TestMethod]
        //public void Test_Preflight()
        //{
        //    var result = _cliExecutor.Preflight();
        //    Assert.IsNotNull(result);
        //}

        //[TestMethod]
        //public async Task Test_Refactor()
        //{
        //    var fileName = "DeepGlobalNestedComplexityExample.js";
        //    var extension = Path.GetExtension(fileName).Replace(".", "");
        //    var baseDir = AppContext.BaseDirectory;
        //    var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\.."));
        //    var issuesJsDir = Path.Combine(projectRoot, @"Codescene.VSExtension.CodeSmells\Issues\Javascript");
        //    string fullPath = Path.Combine(issuesJsDir, fileName);
        //    using (var reader = File.OpenText(fullPath))
        //    {
        //        string content = reader.ReadToEnd();
        //        var result = _cliExecutor.ReviewContent(fileName, content);
        //        var codesmellsJson = JsonConvert.SerializeObject(result.FunctionLevelCodeSmells[0].CodeSmells);
        //        var preflight = _cliExecutor.Preflight();
        //        var refactor = _cliExecutor.FnsToRefactorFromCodeSmells(fileName, content, result.FunctionLevelCodeSmells[0].CodeSmells, preflight);
        //        Assert.IsNotNull(result);
        //    }
        //}

        //[TestMethod]
        //public async Task Test_Post_Refactor()
        //{
        //    var path = "path/to/test/file.cs";
        //    using (var reader = File.OpenText(path))
        //    {
        //        string content = reader.ReadToEnd();
        //        var fileName = Path.GetFileName(path);
        //        var review = _cliExecutor.ReviewContent(fileName, content);
        //        var preflight = _cliExecutor.Preflight();
        //        var refactorableFunctions = _cliExecutor.FnsToRefactorFromCodeSmells(fileName, content, review.FunctionLevelCodeSmells[0].CodeSmells, preflight);
        //        var f = refactorableFunctions.First();
        //        try
        //        {
        //            var refactoredFunctions = _cliExecutor.PostRefactoring(fnToRefactor: f, skipCache: true);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw;
        //        }
        //    }

        //    Assert.IsTrue(true);
        //}
    }
}
