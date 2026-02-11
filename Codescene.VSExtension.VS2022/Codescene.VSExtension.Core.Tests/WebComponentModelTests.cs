// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WebComponentModelTests
    {
        [TestMethod]
        public void AceAcknowledgeComponentData_PropertyAssignment_SetsAllProperties()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "TestFunction" };
            var autoRefactor = new AutoRefactorConfig { Activated = true, Visible = true };

            var model = new AceAcknowledgeComponentData
            {
                FilePath = "src/test.cs",
                AutoRefactor = autoRefactor,
                FnToRefactor = fnToRefactor,
            };

            Assert.AreEqual("src/test.cs", model.FilePath);
            Assert.AreEqual(autoRefactor, model.AutoRefactor);
            Assert.AreEqual(fnToRefactor, model.FnToRefactor);
        }

        [TestMethod]
        public void WebComponentAction_PropertyAssignment_SetsPayload()
        {
            var payload = new WebComponentFileDataBase { FileName = "test.cs" };

            var model = new WebComponentAction { GoToFunctionLocationPayload = payload };

            Assert.AreEqual(payload, model.GoToFunctionLocationPayload);
        }

        [TestMethod]
        public void WebComponentFileData_PropertyAssignment_SetsAllProperties()
        {
            var action = new WebComponentAction();
            var fn = new WebComponentFileDataBaseFn();

            var model = new WebComponentFileData
            {
                FileName = "handler.cs",
                Fn = fn,
                Action = action,
            };

            Assert.AreEqual("handler.cs", model.FileName);
            Assert.AreEqual(fn, model.Fn);
            Assert.AreEqual(action, model.Action);
        }

        [TestMethod]
        public void WebComponentFileData_InheritsFromBase_HasBaseProperties()
        {
            var model = new WebComponentFileData { FileName = "test.cs" };

            WebComponentFileDataBase baseModel = model;

            Assert.AreEqual("test.cs", baseModel.FileName);
        }

        [TestMethod]
        public void WebComponentMessage_PropertyAssignment_SetsAllProperties()
        {
            var payload = new WebComponentPayload<string> { Data = "test data" };

            var model = new WebComponentMessage<string>
            {
                MessageType = "update",
                Payload = payload,
            };

            Assert.AreEqual("update", model.MessageType);
            Assert.AreEqual(payload, model.Payload);
        }

        [TestMethod]
        public void WebComponentMessage_GenericType_WorksWithDifferentTypes()
        {
            var intMessage = new WebComponentMessage<int>
            {
                MessageType = "count",
                Payload = new WebComponentPayload<int> { Data = 42 },
            };

            Assert.AreEqual(42, intMessage.Payload.Data);
        }

        [TestMethod]
        public void WebComponentPayload_PropertyAssignment_SetsAllProperties()
        {
            var model = new WebComponentPayload<string>
            {
                IdeType = "vs2022",
                View = "codehealth",
                Data = "test",
                Pro = true,
                Devmode = true,
            };

            Assert.AreEqual("vs2022", model.IdeType);
            Assert.AreEqual("codehealth", model.View);
            Assert.AreEqual("test", model.Data);
            Assert.IsTrue(model.Pro);
            Assert.IsTrue(model.Devmode);
        }

        [TestMethod]
        public void WebComponentPayload_Pro_DefaultsToFalse()
        {
            var model = new WebComponentPayload<string>();

            Assert.IsFalse(model.Pro);
        }

        [TestMethod]
        public void WebComponentPayload_Devmode_DefaultsToFalse()
        {
            var model = new WebComponentPayload<string>();

            Assert.IsFalse(model.Devmode);
        }

        [TestMethod]
        public void WebComponentPayload_FeatureFlags_HasDefaultValues()
        {
            var model = new WebComponentPayload<string>();

            Assert.IsNotNull(model.FeatureFlags);
            CollectionAssert.Contains(model.FeatureFlags, "jobs");
            CollectionAssert.Contains(model.FeatureFlags, "open-settings");
            CollectionAssert.Contains(model.FeatureFlags, "ace-status-indicator");
        }
    }
}
