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
    }
}
