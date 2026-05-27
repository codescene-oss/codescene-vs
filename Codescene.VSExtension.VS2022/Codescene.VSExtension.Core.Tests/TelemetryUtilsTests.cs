// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryUtilsTests
    {
        [TestMethod]
        public void GetTelemetryEventJson_ReturnsValidJsonWithExpectedFields()
        {
            var json = TelemetryUtils.GetTelemetryEventJson("test-event", "device-123", "1.0.0", "18.1.1");
            var obj = JObject.Parse(json);

            Assert.AreEqual("vs/test-event", obj["event-name"]?.ToString());
            Assert.AreEqual("device-123", obj["user-id"]?.ToString());
            Assert.AreEqual("vs", obj["editor-type"]?.ToString());
            Assert.AreEqual("18.1.1", obj["editor-version"]?.ToString());
            Assert.AreEqual("1.0.0", obj["extension-version"]?.ToString());
        }

        [TestMethod]
        public void GetTelemetryEventJson_WithAdditionalData_MergesProperties()
        {
            var additionalData = new Dictionary<string, object>
            {
                { "custom-key", "custom-value" },
                { "count", 42 },
            };

            var json = TelemetryUtils.GetTelemetryEventJson("test-event", "device-123", "1.0.0", "18.1.1", additionalData);
            var obj = JObject.Parse(json);

            Assert.AreEqual("custom-value", obj["custom-key"]?.ToString());
            Assert.AreEqual(42, obj["count"]?.Value<int>());
            Assert.AreEqual("vs/test-event", obj["event-name"]?.ToString());
            Assert.AreEqual("18.1.1", obj["editor-version"]?.ToString());
        }

        [TestMethod]
        public void GetTelemetryEventJson_OmitsEditorVersion_WhenNull()
        {
            var json = TelemetryUtils.GetTelemetryEventJson("test-event", "device-123", "1.0.0");
            var obj = JObject.Parse(json);

            Assert.IsNull(obj["editor-version"]);
        }

        [TestMethod]
        [DataRow("18.1.1", "18.0")]
        [DataRow("17.12.4", "17.0")]
        [DataRow("18", "18.0")]
        [DataRow("17.0.0", "17.0")]
        public void GetVsCommonRegistryVersionFolder_FromEditorVersion_ReturnsMajorZero(string editorVersion, string expectedFolder)
        {
            Assert.AreEqual(expectedFolder, TelemetryUtils.GetVsCommonRegistryVersionFolder(editorVersion));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("invalid")]
        public void GetVsCommonRegistryVersionFolder_InvalidInput_ReturnsNull(string editorVersion)
        {
            Assert.IsNull(TelemetryUtils.GetVsCommonRegistryVersionFolder(editorVersion));
        }
    }
}
