// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Exceptions;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerClientAuthErrorTests
    {
        private static ContractBundle _bundle = null!;

        private readonly List<ContractClientHarness> _harnesses = new List<ContractClientHarness>();

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var harness in _harnesses)
            {
                harness.Dispose();
            }
        }

        [TestMethod]
        [DataRow("auth-cancelled")]
        [DataRow("auth-invalid-token")]
        [DataRow("auth-network")]
        [DataRow("auth-not-signed-in")]
        [DataRow("auth-oauth-unsupported")]
        [DataRow("auth-port-unavailable")]
        [DataRow("auth-session-revoked")]
        [DataRow("auth-timeout")]
        public async Task Request_FailingWithAnAuthError_SurfacesTheAuthErrorCode(string variant)
        {
            var errorFixture = _bundle.Fixture("json-rpc-error", variant, FixtureForm.Entity);
            var harness = CreateHarness(new AuthErrorServer(errorFixture));
            await harness.Client.StartAsync();

            var ex = await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.AreEqual(-32803, ex.Status);
            Assert.AreEqual((string?)errorFixture["data"]?["code"], ex.AuthErrorCode);
        }

        [TestMethod]
        public async Task Request_FailingWithMethodNotFound_HasStatusMinus32601AndNoAuthCode()
        {
            var harness = CreateHarness(new MethodNotFoundServer());
            await harness.Client.StartAsync();

            var ex = await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.AreEqual(-32601, ex.Status);
            Assert.IsNull(ex.AuthErrorCode);
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("minimal")]
        [DataRow("internal-error")]
        public async Task Request_FailingWithAnInternalError_HasNoAuthCode(string variant)
        {
            var errorFixture = _bundle.Fixture("json-rpc-error", variant, FixtureForm.Entity);
            var harness = CreateHarness(new InternalErrorServer(errorFixture));
            await harness.Client.StartAsync();

            var ex = await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.IsNull(ex.AuthErrorCode);
        }

        [TestMethod]
        public async Task Request_FailingWithConnectionLost_PropagatesConnectionLostExceptionNotDevtoolsException()
        {
            var harness = CreateHarness();
            await harness.Client.StartAsync();
            harness.ServerRpc.Dispose();

            var ex = await Assert.ThrowsAsync<ConnectionLostException>(() => harness.Client.PreflightAsync());

            Assert.IsNotNull(ex);
        }

        [TestMethod]
        public async Task Request_FailingWithAnAuthError_LogsAWarningWithTheCodeAndNoMessage()
        {
            var errorFixture = _bundle.Fixture("json-rpc-error", "auth-cancelled", FixtureForm.Entity);
            var logger = new FakeLogger();
            var harness = CreateHarness(new AuthErrorServer(errorFixture), logger);
            await harness.Client.StartAsync();

            await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.AreEqual(1, logger.Warnings.Count);
            var warning = logger.Warnings[0];
            StringAssert.Contains(warning, "request failed");
            StringAssert.Contains(warning, "code=auth/cancelled");
            Assert.IsFalse(warning.Contains((string?)errorFixture["message"] ?? string.Empty));
            Assert.AreEqual(0, logger.Errors.Count);
        }

        [TestMethod]
        public async Task Request_FailingWithAnInternalError_StillLogsAnError()
        {
            var errorFixture = _bundle.Fixture("json-rpc-error", "internal-error", FixtureForm.Entity);
            var logger = new FakeLogger();
            var harness = CreateHarness(new InternalErrorServer(errorFixture), logger);
            await harness.Client.StartAsync();

            await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.AreEqual(1, logger.Errors.Count);
            StringAssert.Contains(logger.Errors[0], "request failed");
            Assert.AreEqual(0, logger.Warnings.Count);
        }

        [TestMethod]
        public void AuthErrors_Code_ForANonDevtoolsException_IsNull()
        {
            var ex = new Exception("test");

            var code = AuthErrors.Code(ex);

            Assert.IsNull(code);
        }

        [TestMethod]
        public void AuthErrors_IsMethodNotFound_RecognizesStatus32601()
        {
            var ex = new DevtoolsException("Method not found", -32601, null);

            var isMethodNotFound = AuthErrors.IsMethodNotFound(ex);

            Assert.IsTrue(isMethodNotFound);
        }

        [TestMethod]
        public void AuthErrors_IsMethodNotFound_ReturnsFalseForOtherErrors()
        {
            var ex = new DevtoolsException("Internal error", -32603, null);

            var isMethodNotFound = AuthErrors.IsMethodNotFound(ex);

            Assert.IsFalse(isMethodNotFound);
        }

        [TestMethod]
        public async Task Request_FailingWithMethodNotFound_LogsAWarningWithTheCode()
        {
            var logger = new FakeLogger();
            var harness = CreateHarness(new MethodNotFoundServer(), logger);
            await harness.Client.StartAsync();

            await Assert.ThrowsAsync<DevtoolsException>(() => harness.Client.PreflightAsync());

            Assert.AreEqual(1, logger.Warnings.Count);
            StringAssert.Contains(logger.Warnings[0], "code=-32601");
            Assert.AreEqual(0, logger.Errors.Count);
        }

        private ContractClientHarness CreateHarness(object? serverTarget = null, FakeLogger? logger = null)
        {
            var startWire = _bundle.Fixture("start-notification", "minimal", FixtureForm.Wire);
            var harness = ContractClientHarness.Create((JObject)startWire, serverTarget, logger);
            _harnesses.Add(harness);
            return harness;
        }

        private sealed class AuthErrorServer
        {
            private readonly JToken _error;

            public AuthErrorServer(JToken error)
            {
                _error = error;
            }

            [JsonRpcMethod("cs-ide/preflight", UseSingleObjectParameterDeserialization = true)]
            public JObject Preflight(JObject request)
            {
                throw new LocalRpcException((string?)_error["message"] ?? "Auth error")
                {
                    ErrorCode = (int?)_error["code"] ?? -32803,
                    ErrorData = _error["data"]?.DeepClone(),
                };
            }
        }

        private sealed class MethodNotFoundServer
        {
            [JsonRpcMethod("cs-ide/preflight", UseSingleObjectParameterDeserialization = true)]
            public JObject Preflight(JObject request)
            {
                throw new LocalRpcException("Method not found")
                {
                    ErrorCode = -32601,
                };
            }
        }

        private sealed class InternalErrorServer
        {
            private readonly JToken _error;

            public InternalErrorServer(JToken error)
            {
                _error = error;
            }

            [JsonRpcMethod("cs-ide/preflight", UseSingleObjectParameterDeserialization = true)]
            public JObject Preflight(JObject request)
            {
                throw new LocalRpcException((string?)_error["message"] ?? "Internal error")
                {
                    ErrorCode = (int?)_error["code"] ?? -32603,
                    ErrorData = _error["data"]?.DeepClone(),
                };
            }
        }
    }
}
