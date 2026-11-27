// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthCommandActionsTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void IsVisible_Inactive_IsFalse()
        {
            var snapshot = AuthSnapshot.Inactive;

            Assert.IsFalse(AuthCommandActions.IsVisible(AuthCommandActions.Command.SignIn, snapshot));
            Assert.IsFalse(AuthCommandActions.IsVisible(AuthCommandActions.Command.Reauthorize, snapshot));
        }

        [TestMethod]
        public void IsVisible_Active_SignInIsTrue()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            Assert.IsTrue(AuthCommandActions.IsVisible(AuthCommandActions.Command.SignIn, snapshot));
        }

        [TestMethod]
        public void IsVisible_ActiveWithoutReauthorizeNeeded_ReauthorizeIsFalse()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            Assert.IsFalse(AuthCommandActions.IsVisible(AuthCommandActions.Command.Reauthorize, snapshot));
        }

        [TestMethod]
        public void IsVisible_ActiveWithReauthorizeNeeded_ReauthorizeIsTrue()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            status["reauthorize-needed"] = true;
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            Assert.IsTrue(AuthCommandActions.IsVisible(AuthCommandActions.Command.Reauthorize, snapshot));
        }

        [TestMethod]
        public async Task SignIn_WhenSignedOut_OpensAuthAndSignsIn()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var service = new Mock<IAuthService>();
            service.Setup(x => x.Current).Returns(snapshot);

            var host = new Mock<IAuthHost>();

            await AuthCommandActions.SignInAsync(service.Object, host.Object);

            service.Verify(x => x.OpenAuth(null), Times.Once);
            service.Verify(x => x.SignInAsync(), Times.Once);
            host.Verify(x => x.ShowMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task SignIn_WhenSignedIn_OnlyOpensAuth()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var service = new Mock<IAuthService>();
            service.Setup(x => x.Current).Returns(snapshot);

            var host = new Mock<IAuthHost>();

            await AuthCommandActions.SignInAsync(service.Object, host.Object);

            service.Verify(x => x.OpenAuth(null), Times.Once);
            service.Verify(x => x.SignInAsync(), Times.Never);
            host.Verify(x => x.ShowMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [DataRow(nameof(AuthCommandActions.Command.SignIn))]
        [DataRow(nameof(AuthCommandActions.Command.Reauthorize))]
        public async Task AnyCommand_WhileInactive_ShowsTheExperimentalMessage(string commandName)
        {
            var snapshot = AuthSnapshot.Inactive;

            var service = new Mock<IAuthService>();
            service.Setup(x => x.Current).Returns(snapshot);

            var host = new Mock<IAuthHost>();

            switch (commandName)
            {
                case nameof(AuthCommandActions.Command.SignIn):
                    await AuthCommandActions.SignInAsync(service.Object, host.Object);
                    break;
                case nameof(AuthCommandActions.Command.Reauthorize):
                    await AuthCommandActions.ReauthorizeAsync(service.Object, host.Object);
                    break;
            }

            host.Verify(x => x.ShowMessageAsync(AuthCommandActions.InactiveMessage), Times.Once);
        }

        [TestMethod]
        public async Task Reauthorize_WhenNotNeeded_DoesNothing()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var service = new Mock<IAuthService>();
            service.Setup(x => x.Current).Returns(snapshot);

            var host = new Mock<IAuthHost>();

            await AuthCommandActions.ReauthorizeAsync(service.Object, host.Object);

            service.Verify(x => x.ReauthorizeAsync(), Times.Never);
            host.Verify(x => x.ShowMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task Reauthorize_WhenNeeded_CallsReauthorize()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            status["reauthorize-needed"] = true;
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var service = new Mock<IAuthService>();
            service.Setup(x => x.Current).Returns(snapshot);

            var host = new Mock<IAuthHost>();

            await AuthCommandActions.ReauthorizeAsync(service.Object, host.Object);

            service.Verify(x => x.ReauthorizeAsync(), Times.Once);
            host.Verify(x => x.ShowMessageAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
