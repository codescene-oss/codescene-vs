// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentTokenResolverTests
    {
        [TestMethod]
        public async Task Pat_WinsWithoutRequest()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns("pat-token");

            var authClient = new Mock<IIdeServerAuthClient>();
            var logger = new Mock<ILogger>();

            var resolver = new AgentTokenResolver(
                settings.Object,
                AuthSnapshot.Inactive,
                authClient.Object,
                logger.Object);

            var token = await resolver.ResolveAsync(CancellationToken.None);

            Assert.AreEqual("pat-token", token);
            authClient.Verify(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task SignedIn_UsesAuthTokenResult()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns(string.Empty);

            var status = new JObject
            {
                ["state"] = "signed-in",
            };

            var snapshot = new AuthSnapshot(
                true,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false);

            var authClient = new Mock<IIdeServerAuthClient>();
            authClient.Setup(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JObject { ["access-token"] = "oauth-token" });

            var logger = new Mock<ILogger>();

            var resolver = new AgentTokenResolver(
                settings.Object,
                snapshot,
                authClient.Object,
                logger.Object);

            var token = await resolver.ResolveAsync(CancellationToken.None);

            Assert.AreEqual("oauth-token", token);
        }

        [TestMethod]
        public async Task Inactive_NeverSendsRequest()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns(string.Empty);

            var authClient = new Mock<IIdeServerAuthClient>();
            var logger = new Mock<ILogger>();

            var resolver = new AgentTokenResolver(
                settings.Object,
                AuthSnapshot.Inactive,
                authClient.Object,
                logger.Object);

            var token = await resolver.ResolveAsync(CancellationToken.None);

            Assert.IsNull(token);
            authClient.Verify(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task RequestFails_ReturnsNullAndLogsRedacted()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns(string.Empty);

            var status = new JObject
            {
                ["state"] = "signed-in",
            };

            var snapshot = new AuthSnapshot(
                true,
                status,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false);

            var authClient = new Mock<IIdeServerAuthClient>();
            authClient.Setup(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Bearer secret123"));

            var logger = new Mock<ILogger>();

            var resolver = new AgentTokenResolver(
                settings.Object,
                snapshot,
                authClient.Object,
                logger.Object);

            var token = await resolver.ResolveAsync(CancellationToken.None);

            Assert.IsNull(token);
            logger.Verify(l => l.Debug(It.Is<string>(m => m.Contains("Failed to retrieve auth token"))));
            logger.Verify(l => l.Debug(It.Is<string>(m => !m.Contains("secret123"))));
        }

        [TestMethod]
        public async Task ActiveButNotSignedIn_ReturnsNullWithoutRequest()
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns(string.Empty);
            var authClient = new Mock<IIdeServerAuthClient>();
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(new JObject { ["state"] = "signed-out" });

            var resolver = new AgentTokenResolver(settings.Object, snapshot, authClient.Object, new Mock<ILogger>().Object);

            Assert.IsNull(await resolver.ResolveAsync(CancellationToken.None));
            authClient.Verify(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
