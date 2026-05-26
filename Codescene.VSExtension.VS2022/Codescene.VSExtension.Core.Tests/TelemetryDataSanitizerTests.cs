// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryDataSanitizerTests
    {
        [TestMethod]
        public void SanitizeString_WindowsPath_ReplacesWithPathPlaceholder()
        {
            var input = "Failed at C:\\Users\\secret\\repo\\src\\File.cs";

            var result = TelemetryDataSanitizer.SanitizeString(input);

            Assert.Contains("<path>", result);
            Assert.DoesNotContain("secret", result);
        }

        [TestMethod]
        public void SanitizeString_CliToken_RedactsToken()
        {
            var input = "run --token sk-live-abc123 done";

            var result = TelemetryDataSanitizer.SanitizeString(input);

            Assert.Contains("--token ***", result);
            Assert.DoesNotContain("sk-live", result);
        }

        [TestMethod]
        public void SanitizeString_JsonTokenProperty_RedactsValue()
        {
            var input = "{\"token\":\"my-secret-value\"}";

            var result = TelemetryDataSanitizer.SanitizeString(input);

            Assert.Contains(@"""token"":""***""", result);
            Assert.DoesNotContain("my-secret", result);
        }

        [TestMethod]
        public void SanitizeString_UrlWithAccessToken_RedactsQueryValue()
        {
            var input = "https://docs.example.com/page?access_token=abc123&x=1";

            var result = TelemetryDataSanitizer.SanitizeString(input);

            Assert.Contains("access_token=***", result);
            Assert.DoesNotContain("abc123", result);
            Assert.Contains("docs.example.com", result);
        }

        [TestMethod]
        public void SanitizeString_StackFrame_KeepsMethodRedactsPath()
        {
            var input = "   at MyApp.Service.Run() in C:\\Users\\dev\\proj\\File.cs:line 42";

            var result = TelemetryDataSanitizer.SanitizeString(input);

            Assert.Contains("MyApp.Service.Run()", result);
            Assert.Contains("<path>", result);
            Assert.DoesNotContain("dev\\proj", result);
        }

        [TestMethod]
        public void SanitizeDictionary_NestedExtraData_SanitizesStrings()
        {
            var source = new Dictionary<string, object>
            {
                ["message"] = "Error in D:\\work\\app\\Main.cs",
                ["extraData"] = new Dictionary<string, object>
                {
                    ["context"] = "Review failed --token secret-value",
                },
            };

            var result = TelemetryDataSanitizer.SanitizeDictionary(source);

            Assert.Contains("<path>", (string)result["message"]);
            var extraData = (Dictionary<string, object>)result["extraData"];
            Assert.Contains("--token ***", (string)extraData["context"]);
        }

        [TestMethod]
        public void SanitizeJObject_StringProperties_AreRedacted()
        {
            var jObject = JObject.Parse("{\"url\":\"https://x.test/a?token=zzz\",\"count\":3}");

            TelemetryDataSanitizer.SanitizeJObject(jObject);

            var url = jObject["url"]?.ToString();
            Assert.IsNotNull(url);
            Assert.Contains("token=***", url);
            Assert.AreEqual(3, jObject["count"]?.Value<int>());
        }

        [TestMethod]
        public void SerializeException_WithPathInContext_SanitizesContext()
        {
            ErrorTelemetryUtils.ResetErrorCount();
            var ex = new InvalidOperationException("failure");
            var context = "Could not review C:\\Users\\alice\\src\\Foo.cs";

            var result = ErrorTelemetryUtils.SerializeException(ex, context);

            var extraData = (Dictionary<string, object>)result["extraData"];
            Assert.Contains("<path>", (string)extraData["context"]);
        }

        [TestMethod]
        public void SanitizeJObject_Null_DoesNotThrow()
        {
            TelemetryDataSanitizer.SanitizeJObject(null);
        }

        [TestMethod]
        public void SanitizeDictionary_Null_ReturnsNull()
        {
            Assert.IsNull(TelemetryDataSanitizer.SanitizeDictionary(null));
        }

        [TestMethod]
        public void SanitizeDictionary_NullEntryValue_ReturnsNullEntry()
        {
            var source = new Dictionary<string, object> { ["detail"] = null };

            var result = TelemetryDataSanitizer.SanitizeDictionary(source);

            Assert.IsNull(result["detail"]);
        }

        [TestMethod]
        public void SanitizeDictionary_NonStringLeaf_PreservesValue()
        {
            var source = new Dictionary<string, object> { ["count"] = 7, ["flag"] = true };

            var result = TelemetryDataSanitizer.SanitizeDictionary(source);

            Assert.AreEqual(7, result["count"]);
            Assert.IsTrue((bool)result["flag"]);
        }

        [TestMethod]
        public void SanitizeDictionary_SortedDictionary_SanitizesValues()
        {
            var inner = new SortedDictionary<string, object>
            {
                ["context"] = "Failed at D:\\repo\\Main.cs",
            };
            var source = new Dictionary<string, object> { ["payload"] = inner };

            var result = TelemetryDataSanitizer.SanitizeDictionary(source);

            var sanitizedInner = (Dictionary<string, object>)result["payload"];
            Assert.Contains("<path>", (string)sanitizedInner["context"]);
        }

        [TestMethod]
        public void SanitizeDictionary_ListValue_SanitizesEachItem()
        {
            var source = new Dictionary<string, object>
            {
                ["paths"] = new List<object> { "C:\\a\\one.cs", "plain.cs" },
            };

            var result = TelemetryDataSanitizer.SanitizeDictionary(source);
            var paths = (List<object>)result["paths"];

            Assert.Contains("<path>", (string)paths[0]);
            Assert.AreEqual("plain.cs", paths[1]);
        }

        [TestMethod]
        public void SanitizeJObject_NestedObject_SanitizesInnerStrings()
        {
            var jObject = JObject.Parse("{\"outer\":{\"inner\":\"C:\\\\temp\\\\x.cs\"}}");

            TelemetryDataSanitizer.SanitizeJObject(jObject);

            var inner = jObject["outer"]?["inner"]?.ToString();
            Assert.IsNotNull(inner);
            Assert.Contains("<path>", inner);
        }

        [TestMethod]
        public void SanitizeJObject_Array_SanitizesStringElements()
        {
            var jObject = JObject.Parse("{\"items\":[\"C:\\\\a\\\\b.cs\",\"--token abc\"]}");

            TelemetryDataSanitizer.SanitizeJObject(jObject);

            var items = (JArray)jObject["items"];
            Assert.IsNotNull(items);
            var first = items[0]?.ToString();
            var second = items[1]?.ToString();
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.Contains("<path>", first);
            Assert.Contains("--token ***", second);
        }

        [TestMethod]
        public void SanitizeJObject_ArrayWithNullElement_DoesNotThrow()
        {
            var jObject = JObject.Parse("{\"items\":[null,\"ok\"]}");

            TelemetryDataSanitizer.SanitizeJObject(jObject);

            Assert.AreEqual("ok", jObject["items"]?[1]?.ToString());
        }

        [TestMethod]
        public void GetTelemetryEventJson_SanitizesAdditionalProperties()
        {
            var additional = new Dictionary<string, object>
            {
                ["detail"] = "Bearer secret-token-value",
            };

            var json = TelemetryUtils.GetTelemetryEventJson("test-event", "device-1", "1.0.0", null, additional);

            Assert.Contains("Bearer ***", json);
            Assert.DoesNotContain("secret-token-value", json);
        }
    }
}
