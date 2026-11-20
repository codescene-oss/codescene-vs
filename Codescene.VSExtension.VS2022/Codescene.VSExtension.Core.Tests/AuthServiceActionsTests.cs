// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
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
    public sealed class AuthServiceActionsTests
    {
        private static readonly Dictionary<string, Func<AuthService, Task>> Actions = new Dictionary<string, Func<AuthService, Task>>
        {
            ["SignInAsync"] = s => s.SignInAsync(),
            ["ReauthorizeAsync"] = s => s.ReauthorizeAsync(),
            ["OpenBrowserAgainAsync"] = s => s.OpenBrowserAgainAsync(),
            ["OpenAuth"] = s => Run(() => s.OpenAuth("account")),
            ["CloseAuthAsync"] = s => s.CloseAuthAsync(),
            ["SkipProject"] = s => Run(s.SkipProject),
            ["HideSignInBanner"] = s => Run(s.HideSignInBanner),
            ["SignOutAsync"] = s => s.SignOutAsync(),
            ["RetryAsync"] = s => s.RetryAsync(),
            ["SelectAccountAsync"] = s => s.SelectAccountAsync(new JValue("42")),
            ["LinkProjectAsync"] = s => s.LinkProjectAsync(new JValue("101")),
            ["SetAccessTokenAsync"] = s => s.SetAccessTokenAsync("token"),
            ["RemoveAccessTokenAsync"] = s => s.RemoveAccessTokenAsync(),
            ["OpenPatPageAsync"] = s => s.OpenPatPageAsync(),
            ["OpenServerSettingAsync"] = s => s.OpenServerSettingAsync(),
        };

        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void OpenAuth_WithScreen_StartsTheManualFlow()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.SetupSeveralAccounts();
            kit.Start("signed-in");

            kit.Service.OpenAuth("account");

            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Contexts.MANUAL, kit.Service.Current.Context);
            Assert.AreEqual(AuthConstants.Steps.PICKACCOUNT, kit.Service.Current.Step);
            kit.Host.Verify(x => x.RevealMonitorAsync(), Times.Once);

            kit.Service.OpenAuth("project");

            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, kit.Service.Current.Step);
        }

        [TestMethod]
        public async Task OpenAuth_DuringSignIn_KeepsTheSignInFlow()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Login());
            kit.Start();
            await kit.Service.SignInAsync();
            await kit.Service.CloseAuthAsync();
            await kit.Service.SignInAsync();

            kit.Service.OpenAuth("account");

            Assert.AreEqual(AuthConstants.Contexts.SIGNIN, kit.Service.Current.Context);
            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
        }

        [TestMethod]
        public async Task Reauthorize_WhenNeeded_OpensTheUrl()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.AuthClient.Setup(x => x.AuthReauthorizeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Login());
            kit.AuthClient.Setup(x => x.AuthCancelLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Status("signed-in-reauthorize-needed"));
            kit.Start("signed-in-reauthorize-needed");

            await kit.Service.ReauthorizeAsync();
            await kit.Service.CloseAuthAsync();

            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHREAUTHORIZESTARTED, null, It.IsAny<CancellationToken>()), Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync(kit.Login()["authorize-url"].Value<string>()), Times.Once);
            kit.AuthClient.Verify(x => x.AuthCancelLoginAsync("fake-login-id", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Reauthorize_WhenNotNeeded_DoesNothing()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.Start("signed-in");

            await kit.Service.ReauthorizeAsync();

            kit.AuthClient.Verify(x => x.AuthReauthorizeAsync(It.IsAny<CancellationToken>()), Times.Never);
            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHREAUTHORIZESTARTED, It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task Reauthorize_Failure_SendsReauthorizeFailed()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.AuthClient.Setup(x => x.AuthReauthorizeAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("auth error", -32803, null, "auth/network"));
            kit.Start("signed-in-reauthorize-needed");

            await kit.Service.ReauthorizeAsync();

            kit.Telemetry.Verify(
                x => x.SendTelemetryAsync(
                    Constants.Telemetry.AUTHREAUTHORIZEFAILED,
                    It.Is<Dictionary<string, object>>(d => d.Count == 1 && (string)d["error-code"] == "auth/network"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task OpenBrowserAgain_ReopensTheCurrentAuthorizeUrl()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.Start("signing-in");

            await kit.Service.OpenBrowserAgainAsync();

            kit.Host.Verify(x => x.OpenExternalAsync(kit.Status("signing-in")["login"]["authorize-url"].Value<string>()), Times.Once);
        }

        [TestMethod]
        public async Task NonHttpUrl_IsNeverOpened()
        {
            var kit = new AuthServiceKit(_bundle);
            var login = kit.Login();
            login["authorize-url"] = "javascript:alert(1)";
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(login);
            var pending = kit.Status("signing-in");
            pending["login"]["authorize-url"] = "file:///c:/windows/system32/calc.exe";
            kit.Start();

            await kit.Service.SignInAsync();
            kit.RaiseStatus(pending);
            await kit.Service.OpenBrowserAgainAsync();

            kit.Host.Verify(x => x.OpenExternalAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task SetAccessToken_WritesTheSettingAndStartsASignInContext()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.Start();

            await kit.Service.SetAccessTokenAsync("my-token");

            kit.Settings.Verify(x => x.SetAccessTokenAsync("my-token"), Times.Once);
            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHPATSET, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(AuthConstants.Contexts.SIGNIN, kit.Service.Current.Context);
            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
        }

        [TestMethod]
        public async Task RemoveAccessToken_WritesEmpty()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.Start();

            await kit.Service.RemoveAccessTokenAsync();

            kit.Settings.Verify(x => x.SetAccessTokenAsync(string.Empty), Times.Once);
            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHPATREMOVED, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(AuthConstants.Contexts.MANUAL, kit.Service.Current.Context);
        }

        [TestMethod]
        public async Task PatTelemetry_CarriesNoToken()
        {
            var kit = new AuthServiceKit(_bundle);
            var sent = new List<Dictionary<string, object>>();
            kit.Telemetry.Setup(x => x.SendTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .Callback<string, Dictionary<string, object>, CancellationToken>((name, data, token) => sent.Add(data))
                .Returns(Task.CompletedTask);
            kit.Start();

            await kit.Service.SetAccessTokenAsync("secret-token");
            await kit.Service.RemoveAccessTokenAsync();

            Assert.AreEqual(2, sent.Count);
            Assert.IsTrue(sent.TrueForAll(d => d == null), "PAT telemetry must carry no data");
        }

        [TestMethod]
        public async Task Retry_ForcesAnAccountDataReload()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.AuthClient.Setup(x => x.AuthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Status("signed-in"));
            kit.Start("signed-in");
            await kit.IdleAsync();
            kit.AuthClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);

            await kit.Service.RetryAsync();

            kit.AuthClient.Verify(x => x.AuthListAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            kit.AuthClient.Verify(x => x.ProjectsListAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.IsFalse(kit.Service.Current.Syncing);
            Assert.IsNotNull(kit.Service.Current.Accounts);
        }

        [TestMethod]
        public void HideSignInBanner_SetsTheFlagForTheSession()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.Start();

            kit.Service.HideSignInBanner();
            kit.RaiseStatus("signed-out-with-error");

            Assert.IsTrue(kit.Service.Current.SignInBannerHidden);
        }

        [TestMethod]
        [DataRow("SignInAsync")]
        [DataRow("ReauthorizeAsync")]
        [DataRow("OpenBrowserAgainAsync")]
        [DataRow("OpenAuth")]
        [DataRow("CloseAuthAsync")]
        [DataRow("SkipProject")]
        [DataRow("HideSignInBanner")]
        [DataRow("SignOutAsync")]
        [DataRow("RetryAsync")]
        [DataRow("SelectAccountAsync")]
        [DataRow("LinkProjectAsync")]
        [DataRow("SetAccessTokenAsync")]
        [DataRow("RemoveAccessTokenAsync")]
        [DataRow("OpenPatPageAsync")]
        [DataRow("OpenServerSettingAsync")]
        public async Task EveryAction_WhileInactive_IsANoOp(string actionName)
        {
            var kit = new AuthServiceKit(_bundle, enabled: false);
            kit.Start();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));
            kit.Snapshots.Clear();

            await Actions[actionName](kit.Service);

            kit.AuthClient.VerifyAdd(x => x.AuthStatusChanged += It.IsAny<EventHandler<JObject>>(), Times.Once);
            kit.AuthClient.VerifyNoOtherCalls();
            Assert.AreEqual(0, kit.Snapshots.Count);
            Assert.IsFalse(kit.Service.Current.Active);
            kit.Host.VerifyNoOtherCalls();
            kit.Telemetry.VerifyNoOtherCalls();
            kit.Settings.Verify(x => x.SetAccessTokenAsync(It.IsAny<string>()), Times.Never);
        }

        private static Task Run(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
