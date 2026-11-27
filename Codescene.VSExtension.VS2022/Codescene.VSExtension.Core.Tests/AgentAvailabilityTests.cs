// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentAvailabilityTests
    {
        [TestMethod]
        public void Disabled_NotUsable_RegardlessOfAuth()
        {
            var settings = CreateSettingsMock(enabled: false, authToken: "token");
            var authService = CreateAuthServiceMock(active: true, state: AuthConstants.States.SIGNEDIN);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsFalse(availability.Enabled);
            Assert.IsTrue(availability.Authenticated);
            Assert.IsFalse(availability.Usable);
        }

        [TestMethod]
        public void EnabledWithPat_Usable()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: "my-token");
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsTrue(availability.Enabled);
            Assert.IsTrue(availability.Authenticated);
            Assert.IsTrue(availability.Usable);
        }

        [TestMethod]
        public void EnabledOAuthSignedIn_Usable()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authService = CreateAuthServiceMock(active: true, state: AuthConstants.States.SIGNEDIN);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsTrue(availability.Enabled);
            Assert.IsTrue(availability.Authenticated);
            Assert.IsTrue(availability.Usable);
        }

        [TestMethod]
        public void BrowserSignInOff_OnlyPatCounts()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authService = CreateAuthServiceMock(active: false, state: AuthConstants.States.SIGNEDIN);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsTrue(availability.Enabled);
            Assert.IsFalse(availability.Authenticated);
            Assert.IsFalse(availability.Usable);
        }

        [TestMethod]
        public void EnabledNoCredential_NotAuthenticated()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authService = CreateAuthServiceMock(active: true, state: AuthConstants.States.SIGNEDOUT);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsTrue(availability.Enabled);
            Assert.IsFalse(availability.Authenticated);
            Assert.IsFalse(availability.Usable);
        }

        [TestMethod]
        public void AutoRefactor_MapsUsableAndDisabled_ActivatedAlwaysTrue_NoAceStatus()
        {
            var settings1 = CreateSettingsMock(enabled: true, authToken: "token");
            var authService1 = CreateAuthServiceMock(active: false);
            var availability1 = new AgentAvailability(settings1.Object, authService1.Object);

            var config1 = availability1.AutoRefactor();

            Assert.IsTrue(config1.Visible);
            Assert.IsFalse(config1.Disabled);
            Assert.IsTrue(config1.Activated);
            Assert.IsNull(config1.AceStatus);

            var settings2 = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authService2 = CreateAuthServiceMock(active: false);
            var availability2 = new AgentAvailability(settings2.Object, authService2.Object);

            var config2 = availability2.AutoRefactor();

            Assert.IsFalse(config2.Visible);
            Assert.IsTrue(config2.Disabled);
            Assert.IsTrue(config2.Activated);
            Assert.IsNull(config2.AceStatus);
        }

        [TestMethod]
        public void SnapshotChanged_RaisesChangedOnlyWhenStateChanges()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authServiceMock = new Mock<IAuthService>();
            var initialSnapshot = CreateAuthSnapshot(active: true, state: AuthConstants.States.SIGNEDOUT);
            authServiceMock.SetupGet(x => x.Current).Returns(initialSnapshot);

            var availability = new AgentAvailability(settings.Object, authServiceMock.Object);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            var signedInSnapshot = CreateAuthSnapshot(active: true, state: AuthConstants.States.SIGNEDIN);
            authServiceMock.SetupGet(x => x.Current).Returns(signedInSnapshot);
            authServiceMock.Raise(x => x.SnapshotChanged += null, authServiceMock.Object, signedInSnapshot);

            Assert.AreEqual(1, changedCount);

            authServiceMock.Raise(x => x.SnapshotChanged += null, authServiceMock.Object, signedInSnapshot);

            Assert.AreEqual(1, changedCount);

            var signedOutSnapshot = CreateAuthSnapshot(active: true, state: AuthConstants.States.SIGNEDOUT);
            authServiceMock.SetupGet(x => x.Current).Returns(signedOutSnapshot);
            authServiceMock.Raise(x => x.SnapshotChanged += null, authServiceMock.Object, signedOutSnapshot);

            Assert.AreEqual(2, changedCount);
        }

        [TestMethod]
        public void NotifySettingsChanged_RaisesChanged()
        {
            var settingsMock = new Mock<ISettingsProvider>();
            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(false);
            settingsMock.SetupGet(x => x.AuthToken).Returns(string.Empty);
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settingsMock.Object, authService.Object);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(true);
            availability.NotifySettingsChanged();

            Assert.AreEqual(1, changedCount);
        }

        [TestMethod]
        public void AuthTokenChange_ThroughNotify_FlipsUsable()
        {
            var settingsMock = new Mock<ISettingsProvider>();
            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(true);
            settingsMock.SetupGet(x => x.AuthToken).Returns(string.Empty);
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settingsMock.Object, authService.Object);

            Assert.IsFalse(availability.Usable);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            settingsMock.SetupGet(x => x.AuthToken).Returns("new-token");
            availability.NotifySettingsChanged();

            Assert.AreEqual(1, changedCount);
            Assert.IsTrue(availability.Usable);
        }

        [TestMethod]
        public void EnableToggle_ThroughNotify_FlipsUsable()
        {
            var settingsMock = new Mock<ISettingsProvider>();
            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(false);
            settingsMock.SetupGet(x => x.AuthToken).Returns("token");
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settingsMock.Object, authService.Object);

            Assert.IsFalse(availability.Usable);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(true);
            availability.NotifySettingsChanged();

            Assert.AreEqual(1, changedCount);
            Assert.IsTrue(availability.Usable);
        }

        [TestMethod]
        public void SnapshotChanged_NoChangeInState_DoesNotRaiseChanged()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: "token");
            var authServiceMock = new Mock<IAuthService>();
            var snapshot1 = CreateAuthSnapshot(active: true, state: AuthConstants.States.SIGNEDIN);
            authServiceMock.SetupGet(x => x.Current).Returns(snapshot1);

            var availability = new AgentAvailability(settings.Object, authServiceMock.Object);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            var snapshot2 = CreateAuthSnapshot(active: true, state: AuthConstants.States.SIGNEDIN);
            authServiceMock.Raise(x => x.SnapshotChanged += null, authServiceMock.Object, snapshot2);

            Assert.AreEqual(0, changedCount);
        }

        [TestMethod]
        public void EnabledChange_FromFalseToTrue_RaisesChanged()
        {
            var settingsMock = new Mock<ISettingsProvider>();
            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(false);
            settingsMock.SetupGet(x => x.AuthToken).Returns("token");
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settingsMock.Object, authService.Object);

            Assert.IsFalse(availability.Enabled);
            Assert.IsFalse(availability.Usable);

            var changedCount = 0;
            availability.Changed += (sender, args) => changedCount++;

            settingsMock.SetupGet(x => x.EnableRefactoringAgent).Returns(true);
            availability.NotifySettingsChanged();

            Assert.AreEqual(1, changedCount);
            Assert.IsTrue(availability.Enabled);
            Assert.IsTrue(availability.Usable);
        }

        [TestMethod]
        public void WhitespaceAuthToken_NotAuthenticated()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: "   ");
            var authService = CreateAuthServiceMock(active: false);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsFalse(availability.Authenticated);
            Assert.IsFalse(availability.Usable);
        }

        [TestMethod]
        public void ActiveButSigningIn_NotAuthenticated()
        {
            var settings = CreateSettingsMock(enabled: true, authToken: string.Empty);
            var authService = CreateAuthServiceMock(active: true, state: AuthConstants.States.SIGNINGIN);

            var availability = new AgentAvailability(settings.Object, authService.Object);

            Assert.IsTrue(availability.Enabled);
            Assert.IsFalse(availability.Authenticated);
            Assert.IsFalse(availability.Usable);
        }

        private static Mock<ISettingsProvider> CreateSettingsMock(bool enabled = true, string authToken = null)
        {
            var mock = new Mock<ISettingsProvider>();
            mock.SetupGet(x => x.EnableRefactoringAgent).Returns(enabled);
            mock.SetupGet(x => x.AuthToken).Returns(authToken ?? string.Empty);
            return mock;
        }

        private static Mock<IAuthService> CreateAuthServiceMock(bool active = false, string state = null)
        {
            var mock = new Mock<IAuthService>();
            var snapshot = CreateAuthSnapshot(active, state);
            mock.SetupGet(x => x.Current).Returns(snapshot);
            return mock;
        }

        private static AuthSnapshot CreateAuthSnapshot(bool active, string state)
        {
            JObject status = null;
            if (active && state != null)
            {
                status = new JObject { ["state"] = state };
            }

            return new AuthSnapshot(
                active,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                new JArray(),
                AuthConstants.Surfaces.MONITOR,
                AuthConstants.Steps.SUMMARY,
                AuthConstants.Contexts.MANUAL,
                false,
                false);
        }
    }
}
