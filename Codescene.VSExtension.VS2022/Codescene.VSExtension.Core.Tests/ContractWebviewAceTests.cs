// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.Core.Util;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ContractWebviewAceTests
    {
        private static CwfSchemas _cwf = null!;
        private Mock<ILogger> _loggerMock;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _cwf = CwfSchemas.Locate();
        }

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [TestMethod]
        public void MappedResponse_FixProposed_ValidatesAsRefactorResponse()
        {
            var output = LoadFixture("fix-proposed.json");
            var fn = CreateTestFunction("if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }");
            var documentText = "if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }";

            var response = AgentOutputMapper.Map(output, documentText, fn, "test.ts", _loggerMock.Object);
            var payload = CreateAcePayload(response, fn, "test.ts");

            AssertValidAndClosed(payload);
        }

        [TestMethod]
        public void MappedResponse_UnableToFix_ValidatesAsRefactorResponse()
        {
            var output = LoadFixture("unable-to-fix.json");
            var fn = CreateTestFunction("test code");
            var documentText = "test code";

            var response = AgentOutputMapper.Map(output, documentText, fn, "test.ts", _loggerMock.Object);
            var payload = CreateAcePayload(response, fn, "test.ts");

            AssertValidAndClosed(payload);
        }

        [TestMethod]
        public void MappedResponse_NeedsHumanReview_ValidatesAsRefactorResponse()
        {
            var output = LoadFixture("needs-human-review.json");
            var fn = CreateTestFunction("try {\n    doSomething();\n  } catch (e) {\n    handleError(e);\n  }");
            var documentText = "try {\n    doSomething();\n  } catch (e) {\n    handleError(e);\n  }";

            var response = AgentOutputMapper.Map(output, documentText, fn, "test.ts", _loggerMock.Object);
            var payload = CreateAcePayload(response, fn, "test.ts");

            AssertValidAndClosed(payload);
        }

        [TestMethod]
        public void AcePayload_Loading_ValidAndClosed()
        {
            var fn = CreateTestFunction("test code");
            var aceData = new AceComponentData
            {
                Loading = true,
                Error = null,
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = null,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_Progress_ValidAndClosed()
        {
            var fn = CreateTestFunction("test code");
            var aceData = new AceComponentData
            {
                Loading = true,
                Error = null,
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = null,
                ProgressMessage = "Analyzing code...",
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_Result_ValidAndClosed()
        {
            var output = LoadFixture("fix-proposed.json");
            var fn = CreateTestFunction("if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }");
            var documentText = "if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }";
            var response = AgentOutputMapper.Map(output, documentText, fn, "test.ts", _loggerMock.Object);

            var aceData = new AceComponentData
            {
                Loading = false,
                Error = null,
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = response,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_AuthError_ValidAndClosed()
        {
            var fn = CreateTestFunction("test code");
            var aceData = new AceComponentData
            {
                Loading = false,
                Error = "auth",
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = null,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_GenericError_ValidAndClosed()
        {
            var fn = CreateTestFunction("test code");
            var aceData = new AceComponentData
            {
                Loading = false,
                Error = "generic",
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = null,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_Stale_ValidAndClosed()
        {
            var output = LoadFixture("fix-proposed.json");
            var fn = CreateTestFunction("if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }");
            var documentText = "if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }";
            var response = AgentOutputMapper.Map(output, documentText, fn, "test.ts", _loggerMock.Object);

            var aceData = new AceComponentData
            {
                Loading = false,
                Error = null,
                IsStale = true,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = response,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            AssertValidAndClosed(sent);
        }

        [TestMethod]
        public void AcePayload_HasNoTopLevelFnToRefactor()
        {
            var fn = CreateTestFunction("test code");
            var aceData = new AceComponentData
            {
                Loading = false,
                Error = null,
                IsStale = false,
                FileData = CreateFileData(fn, "test.ts"),
                AceResultData = null,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            var sent = JObject.Parse(WebComponentJson.Serialize(payload));

            var data = (JObject)sent["data"];
            Assert.IsFalse(data.ContainsKey("fnToRefactor"), "AceComponentData should not have a top-level fnToRefactor property");
        }

        private static void AssertCasingOfAcePayload(JObject sent)
        {
            if (sent?["data"] == null)
            {
                throw new System.InvalidOperationException("sent or sent['data'] is null");
            }

            var viewProps = (JObject)sent["data"].DeepClone();
            var aceResultData = Detach(viewProps, "aceResultData");
            var fnToRefactor = Detach(viewProps["fileData"] as JObject, "fnToRefactor");

            ContractBundle.AssertCasing(viewProps, Casing.Camel);
            AssertKebabUnlessNull(fnToRefactor);
            AssertKebabUnlessNull(aceResultData);
        }

        private static JToken? Detach(JObject? parent, string name)
        {
            if (parent == null || !parent.ContainsKey(name))
            {
                return null;
            }

            var value = parent[name];
            parent.Remove(name);
            return value;
        }

        private static void AssertKebabUnlessNull(JToken? token)
        {
            if (token != null && token.Type != JTokenType.Null)
            {
                ContractBundle.AssertCasing(token, Casing.Kebab);
            }
        }

        private static void AssertFails(Action validate, string expectedInMessage)
        {
            var failure = Assert.Throws<AssertFailedException>(validate);
            StringAssert.Contains(failure.Message, expectedInMessage);
        }

        private AgentOutput LoadFixture(string fileName)
        {
            var fixturePath = System.IO.Path.Combine("Fixtures", "AgentOutput", fileName);
            var json = System.IO.File.ReadAllText(fixturePath);
            return JsonConvert.DeserializeObject<AgentOutput>(json) ?? throw new System.InvalidOperationException("Fixture " + fileName + " is empty");
        }

        private FnToRefactorModel CreateTestFunction(string body)
        {
            return new FnToRefactorModel
            {
                Name = "testFunction",
                Body = body,
                FileType = "ts",
                FunctionType = "ts",
                Range = new CliRangeModel
                {
                    StartLine = 1,
                    StartColumn = 1,
                    EndLine = 1,
                    EndColumn = body.Length + 1,
                },
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "complex-method", Line = 1 },
                },
            };
        }

        private JObject CreateAcePayload(RefactorResponseModel response, FnToRefactorModel fn, string path)
        {
            var aceData = new AceComponentData
            {
                Loading = false,
                Error = null,
                IsStale = false,
                FileData = CreateFileData(fn, path),
                AceResultData = response,
                ProgressMessage = null,
            };

            var payload = WebComponentPayload<AceComponentData>.Create(ViewTypes.ACE, aceData);
            return JObject.Parse(WebComponentJson.Serialize(payload));
        }

        private WebComponentFileDataBase CreateFileData(FnToRefactorModel fn, string path)
        {
            return new WebComponentFileDataBase
            {
                FileName = Path.GetFileName(path),
                Fn = new WebComponentFileDataBaseFn
                {
                    Name = fn.Name,
                    Range = new CodeRangeModel(
                        fn.Range.StartLine,
                        fn.Range.EndLine,
                        fn.Range.StartColumn,
                        fn.Range.EndColumn),
                },
                FnToRefactor = fn,
            };
        }

        private void AssertValidAndClosed(JObject sent)
        {
            AssertCasingOfAcePayload(sent);
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(sent));

            var topLevel = (JObject)sent.DeepClone();
            topLevel["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(topLevel)), "undeclared");

            var nested = (JObject)sent.DeepClone();
            ((JObject)nested["data"])["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(nested)), "undeclared");

            var aceResultData = sent.SelectToken("data.aceResultData");
            if (aceResultData != null)
            {
                var inResponse = (JObject)sent.DeepClone();
                ((JObject)inResponse.SelectToken("data.aceResultData"))["undeclared"] = true;
                AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(inResponse)), "undeclared");
            }
        }
    }
}
