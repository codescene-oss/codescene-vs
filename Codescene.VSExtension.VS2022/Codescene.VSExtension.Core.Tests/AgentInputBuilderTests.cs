// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentInputBuilderTests
    {
        [TestMethod]
        public void UsesFirstTarget()
        {
            var fn = new FnToRefactorModel
            {
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "complex-method", Line = 10 },
                    new RefactoringTargetModel { Category = "nested-complexity", Line = 20 },
                },
            };

            var result = AgentInputBuilder.ForRefactoring("task-123", "C:/test/file.cs", fn);

            Assert.AreEqual(10, result.Line);
            Assert.AreEqual("complex-method", result.Smell);
        }

        [TestMethod]
        public void SerializesVsCodeShape()
        {
            var fn = new FnToRefactorModel
            {
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "complex-method", Line = 10 },
                },
            };

            var result = AgentInputBuilder.ForRefactoring("task-123", "C:/test/file.cs", fn);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);

            Assert.IsTrue(json.Contains("\"@context\""));
            Assert.IsTrue(json.Contains("\"task_id\""));
            Assert.IsTrue(json.Contains("\"file\""));
            Assert.IsTrue(json.Contains("\"line\""));
            Assert.IsTrue(json.Contains("\"smell\""));
            Assert.IsTrue(json.Contains("https://codescene.io/schemas/code-health-fix#"));
            Assert.AreEqual("task-123", result.TaskId);
            Assert.AreEqual("C:/test/file.cs", result.File);
        }

        [TestMethod]
        public void NoTargets_FailsGeneric()
        {
            var fn = new FnToRefactorModel
            {
                RefactoringTargets = new RefactoringTargetModel[0],
            };

            try
            {
                AgentInputBuilder.ForRefactoring("task-123", "C:/test/file.cs", fn);
                Assert.Fail("Expected AgentFailure to be thrown");
            }
            catch (AgentFailure ex)
            {
                Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
                Assert.IsTrue(ex.Message.Contains("No refactoring targets"));
            }
        }
    }
}
