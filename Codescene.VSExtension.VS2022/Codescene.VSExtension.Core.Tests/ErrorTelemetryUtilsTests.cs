using Codescene.VSExtension.Core.Util;
using System.Net.Sockets;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ErrorTelemetryUtilsTests
    {
        [TestInitialize]
        public void Setup()
        {
            ErrorTelemetryUtils.ResetErrorCount();
        }

        [TestMethod]
        public void ShouldSendError_FirstError_ReturnsTrue()
        {
            var ex = new Exception("Test error");
            Assert.IsTrue(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_AfterMaxErrors_ReturnsFalse()
        {
            var ex = new Exception("Test error");

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(ErrorTelemetryUtils.ShouldSendError(ex));
                ErrorTelemetryUtils.IncrementErrorCount();
            }

            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_TelemetryRelatedError_ReturnsFalse()
        {
            var ex = new Exception("Failed to send telemetry event");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_TelemetryInMessage_CaseInsensitive_ReturnsFalse()
        {
            var ex = new Exception("TELEMETRY service unavailable");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_JavaConnectException_ReturnsFalse()
        {
            var ex = new Exception("java.net.ConnectException: Connection refused");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_ECONNREFUSED_ReturnsFalse()
        {
            var ex = new Exception("ECONNREFUSED: Connection refused");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_GetAddrInfoNotFound_ReturnsFalse()
        {
            var ex = new Exception("getaddrinfo ENOTFOUND api.codescene.io");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_SocketException_ReturnsFalse()
        {
            var ex = new SocketException();
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_InInnerException_ReturnsFalse()
        {
            var innerEx = new Exception("ECONNREFUSED");
            var ex = new Exception("Outer error", innerEx);
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_Status5xx_ReturnsFalse()
        {
            var ex = new Exception("Server returned status 500");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ShouldSendError_NetworkError_NoSuchHost_ReturnsFalse()
        {
            var ex = new Exception("No such host is known");
            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void SerializeException_BasicException_ReturnsCorrectFormat()
        {
            var ex = new InvalidOperationException("Test message");
            var result = ErrorTelemetryUtils.SerializeException(ex, "Test context");

            Assert.AreEqual("InvalidOperationException", result["name"]);
            Assert.AreEqual("Test message", result["message"]);
            Assert.IsTrue(result.ContainsKey("extraData"));

            var extraData = (Dictionary<string, object>)result["extraData"];
            Assert.AreEqual("Test context", extraData["context"]);
        }

        [TestMethod]
        public void SerializeException_WithStackTrace_IncludesStack()
        {
            Exception ex;
            try
            {
                throw new Exception("Test error");
            }
            catch (Exception e)
            {
                ex = e;
            }

            var result = ErrorTelemetryUtils.SerializeException(ex, "context");

            Assert.IsTrue(result.ContainsKey("stack"));
            Assert.IsTrue(((string)result["stack"]).Contains("SerializeException_WithStackTrace_IncludesStack"));
        }

        [TestMethod]
        public void SerializeException_WithInnerException_IncludesInnerDetails()
        {
            var innerEx = new ArgumentNullException("paramName");
            var ex = new InvalidOperationException("Outer message", innerEx);

            var result = ErrorTelemetryUtils.SerializeException(ex, "context");

            var extraData = (Dictionary<string, object>)result["extraData"];
            Assert.AreEqual("ArgumentNullException", extraData["innerExceptionName"]);
            Assert.IsTrue(((string)extraData["innerExceptionMessage"]).Contains("paramName"));
        }

        [TestMethod]
        public void SerializeException_NullContext_SetsEmptyString()
        {
            var ex = new Exception("Test");
            var result = ErrorTelemetryUtils.SerializeException(ex, null);

            var extraData = (Dictionary<string, object>)result["extraData"];
            Assert.AreEqual(string.Empty, extraData["context"]);
        }

        [TestMethod]
        public void SerializeException_EmptyMessage_PreservesEmptyString()
        {
            var ex = new Exception(string.Empty);
            var result = ErrorTelemetryUtils.SerializeException(ex, "context");

            Assert.AreEqual(string.Empty, result["message"]);
        }

        [TestMethod]
        public void IncrementErrorCount_IncreasesCount()
        {
            var ex = new Exception("Test");

            Assert.IsTrue(ErrorTelemetryUtils.ShouldSendError(ex));
            ErrorTelemetryUtils.IncrementErrorCount();
            ErrorTelemetryUtils.IncrementErrorCount();
            ErrorTelemetryUtils.IncrementErrorCount();
            ErrorTelemetryUtils.IncrementErrorCount();
            ErrorTelemetryUtils.IncrementErrorCount();

            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));
        }

        [TestMethod]
        public void ResetErrorCount_ResetsToZero()
        {
            var ex = new Exception("Test");

            for (int i = 0; i < 5; i++)
                ErrorTelemetryUtils.IncrementErrorCount();

            Assert.IsFalse(ErrorTelemetryUtils.ShouldSendError(ex));

            ErrorTelemetryUtils.ResetErrorCount();

            Assert.IsTrue(ErrorTelemetryUtils.ShouldSendError(ex));
        }
    }
}
