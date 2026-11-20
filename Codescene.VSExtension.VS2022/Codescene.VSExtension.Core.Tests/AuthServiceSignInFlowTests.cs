// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public sealed class AuthServiceSignInFlowTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task SignIn_ShowsAuthSurfaceRevealsMonitorAndOpensTheAuthorizeUrl()
        {
            var kit = new AuthServiceKit(_bundle);
            var login = new TaskCompletionSource<JObject>();
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).Returns(login.Task);
            kit.Start();

            var signIn = kit.Service.SignInAsync();

            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Contexts.SIGNIN, kit.Service.Current.Context);
            kit.Host.Verify(x => x.RevealMonitorAsync(), Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync(It.IsAny<string>()), Times.Never);

            login.SetResult(kit.Login());
            await signIn;

            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINSTARTED, null, It.IsAny<CancellationToken>()), Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync(kit.Login()["authorize-url"].Value<string>()), Times.Once);
        }

        [TestMethod]
        public async Task SignIn_ThenSignedInStatus_ReturnsToMonitorAfterResolution()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData("remote");
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Login());
            kit.Start();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));

            await kit.Service.SignInAsync();
            kit.RaiseStatus("signing-in");
            kit.RaiseStatus("signed-in");
            await kit.IdleAsync();

            Assert.IsTrue(kit.Snapshots.Any(s => s.Step == AuthConstants.Steps.RESOLVING && s.Syncing));
            kit.AuthClient.Verify(x => x.AuthSyncAccountAsync("/repo", It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(AuthConstants.Surfaces.MONITOR, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Steps.SUMMARY, kit.Service.Current.Step);
            Assert.AreEqual(AuthConstants.Contexts.MANUAL, kit.Service.Current.Context);
            Assert.IsFalse(kit.Service.Current.Syncing);
            Assert.AreEqual("remote", kit.Service.Current.ProjectSource);
        }

        [TestMethod]
        public async Task SignIn_ClosedWhileLoginInFlight_CancelsTheLoginAndOpensNothing()
        {
            var kit = new AuthServiceKit(_bundle);
            var login = new TaskCompletionSource<JObject>();
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).Returns(login.Task);
            kit.AuthClient.Setup(x => x.AuthCancelLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Status("signed-out"));
            kit.Start();

            var signIn = kit.Service.SignInAsync();
            await kit.Service.CloseAuthAsync();
            login.SetResult(kit.Login());
            await signIn;

            kit.Host.Verify(x => x.OpenExternalAsync(It.IsAny<string>()), Times.Never);
            kit.AuthClient.Verify(x => x.AuthCancelLoginAsync("fake-login-id", It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(AuthConstants.Surfaces.MONITOR, kit.Service.Current.Surface);
        }

        [TestMethod]
        public async Task SignIn_SupersededByANewerSignIn_CancelsTheOlderLogin()
        {
            var kit = new AuthServiceKit(_bundle);
            var first = new TaskCompletionSource<JObject>();
            var second = new TaskCompletionSource<JObject>();
            var logins = new Queue<Task<JObject>>(new[] { first.Task, second.Task });
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).Returns(() => logins.Dequeue());
            kit.AuthClient.Setup(x => x.AuthCancelLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Status("signed-out"));
            kit.Start();

            var signIn1 = kit.Service.SignInAsync();
            var signIn2 = kit.Service.SignInAsync();
            first.SetResult(LoginWith("login-1", "https://cloud.example.invalid/auth1"));
            second.SetResult(LoginWith("login-2", "https://cloud.example.invalid/auth2"));
            await Task.WhenAll(signIn1, signIn2);

            kit.AuthClient.Verify(x => x.AuthCancelLoginAsync("login-1", It.IsAny<CancellationToken>()), Times.Once);
            kit.AuthClient.Verify(x => x.AuthCancelLoginAsync("login-2", It.IsAny<CancellationToken>()), Times.Never);
            kit.Host.Verify(x => x.OpenExternalAsync("https://cloud.example.invalid/auth2"), Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync("https://cloud.example.invalid/auth1"), Times.Never);
        }

        [TestMethod]
        public async Task SignIn_Failure_SendsSignInFailedWithTheCode()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("auth error", -32803, null, "auth/port-unavailable"));
            kit.Start();

            await kit.Service.SignInAsync();

            kit.Telemetry.Verify(
                x => x.SendTelemetryAsync(
                    Constants.Telemetry.AUTHSIGNINFAILED,
                    It.Is<Dictionary<string, object>>(d => d.Count == 1 && (string)d["error-code"] == "auth/port-unavailable"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task SignInOutcomeTelemetry_OnCompletedCancelledFailed()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData("remote");
            kit.Start();
            var cancelled = kit.Status("signed-out");
            cancelled["error"] = new JObject { ["code"] = "auth/cancelled", ["message"] = "Sign-in was cancelled." };

            kit.RaiseStatus("signing-in");
            kit.RaiseStatus("signed-in");
            kit.RaiseStatus("signing-in");
            kit.RaiseStatus(cancelled);
            kit.RaiseStatus("signing-in");
            kit.RaiseStatus("signed-out-timeout");
            await kit.IdleAsync();

            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINCOMPLETED, null, It.IsAny<CancellationToken>()), Times.Once);
            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINCANCELLED, null, It.IsAny<CancellationToken>()), Times.Once);
            kit.Telemetry.Verify(
                x => x.SendTelemetryAsync(
                    Constants.Telemetry.AUTHSIGNINFAILED,
                    It.Is<Dictionary<string, object>>(d => (string)d["error-code"] == "auth/timeout"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static JObject LoginWith(string loginId, string authorizeUrl)
        {
            var login = (JObject)_bundle.Fixture("login-pending", "full", FixtureForm.Wire);
            login["login-id"] = loginId;
            login["authorize-url"] = authorizeUrl;
            return login;
        }
    }
}
