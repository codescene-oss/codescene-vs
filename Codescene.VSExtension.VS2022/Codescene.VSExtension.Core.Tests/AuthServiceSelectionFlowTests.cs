// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public sealed class AuthServiceSelectionFlowTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public async Task SignIn_WithSeveralAccountsAndNoLink_GoesToPickAccount()
        {
            var kit = await SignedInThroughTheFlowAsync();

            Assert.AreEqual(AuthConstants.Steps.PICKACCOUNT, kit.Service.Current.Step);
            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Contexts.SIGNIN, kit.Service.Current.Context);
            Assert.AreEqual("other-account", kit.Service.Current.ProjectReason);
        }

        [TestMethod]
        public async Task SelectAccount_ThenRepoWithProjects_GoesToPickProject()
        {
            var kit = await SignedInThroughTheFlowAsync();
            kit.AuthClient.Setup(x => x.AuthSelectAccountAsync(It.IsAny<JToken>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Status("signed-in"));

            await kit.Service.SelectAccountAsync(new JValue(43));
            await kit.IdleAsync();

            kit.AuthClient.Verify(x => x.AuthSelectAccountAsync(It.Is<JToken>(t => t.Type == JTokenType.Integer && (int)t == 43), It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, kit.Service.Current.Step);
            Assert.AreEqual(AuthConstants.Surfaces.AUTH, kit.Service.Current.Surface);
        }

        [TestMethod]
        public async Task SkipProject_EndsTheFlow()
        {
            var kit = await AtPickProjectAsync();

            kit.Service.SkipProject();

            Assert.AreEqual(AuthConstants.Surfaces.MONITOR, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Steps.SUMMARY, kit.Service.Current.Step);
            Assert.IsNull(kit.Service.Current.LinkedProject);
        }

        [TestMethod]
        public async Task LinkProject_EndsTheFlowWithSourceLink()
        {
            var kit = await AtPickProjectAsync();
            kit.AuthClient.Setup(x => x.ProjectLinkSetAsync(It.IsAny<string>(), It.IsAny<JToken>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Link("remote"));

            await kit.Service.LinkProjectAsync(new JValue("101"));

            kit.AuthClient.Verify(x => x.ProjectLinkSetAsync("/repo", It.Is<JToken>(t => (string)t == "101"), It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual("link", kit.Service.Current.ProjectSource);
            Assert.AreEqual("101", (string)kit.Service.Current.LinkedProject["id"]);
            Assert.AreEqual(AuthConstants.Surfaces.MONITOR, kit.Service.Current.Surface);
            Assert.AreEqual(AuthConstants.Steps.SUMMARY, kit.Service.Current.Step);
        }

        [TestMethod]
        public async Task SelectAccount_ReturningALogin_OpensItAndSendsSignInStarted()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.Start("signed-in");
            await kit.IdleAsync();
            var status = kit.Status("signing-in");
            kit.AuthClient.Setup(x => x.AuthSelectAccountAsync(It.IsAny<JToken>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
            kit.Service.OpenAuth("account");

            await kit.Service.SelectAccountAsync(new JValue("43"));

            kit.Telemetry.Verify(x => x.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINSTARTED, null, It.IsAny<CancellationToken>()), Times.Once);
            kit.Host.Verify(x => x.OpenExternalAsync(status["login"]["authorize-url"].Value<string>()), Times.Once);
            kit.AuthClient.Verify(x => x.AuthCancelLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task LinkProjectNull_SetsThenReReadsTheLink()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData("remote");
            kit.Start("signed-in");
            await kit.IdleAsync();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));
            kit.AuthClient.Setup(x => x.ProjectLinkSetAsync(It.IsAny<string>(), It.IsAny<JToken>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(kit.Link("minimal"));

            await kit.Service.LinkProjectAsync(JValue.CreateNull());

            kit.AuthClient.Verify(x => x.ProjectLinkSetAsync("/repo", null, It.IsAny<CancellationToken>()), Times.Once);
            kit.AuthClient.Verify(x => x.ProjectLinkGetAsync("/repo", It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual("remote", kit.Service.Current.ProjectSource);
        }

        [TestMethod]
        public async Task LinkProject_WithoutRepo_IsANoOp()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.Start("signed-in");
            await kit.IdleAsync();

            await kit.Service.LinkProjectAsync(new JValue("101"));

            kit.AuthClient.Verify(x => x.ProjectLinkSetAsync(It.IsAny<string>(), It.IsAny<JToken>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task LinkProject_ForAReplacedRepo_DoesNotOverwrite()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.Start("signed-in");
            await kit.IdleAsync();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo1", "repo1"));
            var set = new TaskCompletionSource<JObject>();
            kit.AuthClient.Setup(x => x.ProjectLinkSetAsync("/repo1", It.IsAny<JToken>(), It.IsAny<CancellationToken>())).Returns(set.Task);

            var link = kit.Service.LinkProjectAsync(new JValue("101"));
            await kit.Service.SetRepoAsync(new AuthRepo("/repo2", "repo2"));
            set.SetResult(kit.Link("remote"));
            await link;

            Assert.AreEqual("/repo2", kit.Service.Current.Repo.Path);
            Assert.IsNull(kit.Service.Current.LinkedProject);
            Assert.AreEqual("other-account", kit.Service.Current.ProjectReason);
        }

        private static async Task<AuthServiceKit> SignedInThroughTheFlowAsync()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.SetupSeveralAccounts();
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Login());
            kit.Start();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));
            await kit.Service.SignInAsync();
            kit.RaiseStatus("signed-in");
            await kit.IdleAsync();
            return kit;
        }

        private static async Task<AuthServiceKit> AtPickProjectAsync()
        {
            var kit = new AuthServiceKit(_bundle);
            kit.SetupAccountData();
            kit.AuthClient.Setup(x => x.AuthLoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(kit.Login());
            kit.Start();
            await kit.Service.SetRepoAsync(new AuthRepo("/repo", "repo"));
            await kit.Service.SignInAsync();
            kit.RaiseStatus("signed-in");
            await kit.IdleAsync();
            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, kit.Service.Current.Step);
            return kit;
        }
    }
}
