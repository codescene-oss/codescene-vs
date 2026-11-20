// Copyright (c) CodeScene. All rights reserved.

using System.Linq.Expressions;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public sealed class AuthMessageRouterTests
    {
        private static readonly Dictionary<string, Expression<Action<IAuthService>>> ExpectedCalls = new Dictionary<string, Expression<Action<IAuthService>>>
        {
            { WebComponentConstants.MessageTypes.OPENAUTH, x => x.OpenAuth("account") },
            { WebComponentConstants.MessageTypes.AUTHCLOSE, x => x.CloseAuthAsync() },
            { WebComponentConstants.MessageTypes.AUTHSKIPPROJECT, x => x.SkipProject() },
            { WebComponentConstants.MessageTypes.HIDESIGNINBANNER, x => x.HideSignInBanner() },
            { WebComponentConstants.MessageTypes.AUTHSIGNIN, x => x.SignInAsync() },
            { WebComponentConstants.MessageTypes.AUTHOPENBROWSERAGAIN, x => x.OpenBrowserAgainAsync() },
            { WebComponentConstants.MessageTypes.AUTHSIGNOUT, x => x.SignOutAsync() },
            { WebComponentConstants.MessageTypes.AUTHRETRY, x => x.RetryAsync() },
            { WebComponentConstants.MessageTypes.AUTHSELECTACCOUNT, x => x.SelectAccountAsync(It.IsAny<JToken>()) },
            { WebComponentConstants.MessageTypes.AUTHLINKPROJECT, x => x.LinkProjectAsync(It.IsAny<JToken>()) },
            { WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN, x => x.SetAccessTokenAsync("tok-123") },
            { WebComponentConstants.MessageTypes.AUTHREMOVEACCESSTOKEN, x => x.RemoveAccessTokenAsync() },
            { WebComponentConstants.MessageTypes.AUTHOPENSERVERSETTING, x => x.OpenServerSettingAsync() },
            { WebComponentConstants.MessageTypes.AUTHOPENPATPAGE, x => x.OpenPatPageAsync() },
            { WebComponentConstants.MessageTypes.AUTHREAUTHORIZE, x => x.ReauthorizeAsync() },
        };

        [TestMethod]
        [DataRow(WebComponentConstants.MessageTypes.INIT)]
        [DataRow(null)]
        public async Task NonAuthMessage_IsNotRouted(string messageType)
        {
            var authService = new Mock<IAuthService>();
            var logger = new FakeLogger();
            var router = new AuthMessageRouter(authService.Object, logger);

            var routed = await router.TryRouteAsync(messageType, null);

            Assert.IsFalse(routed);
            authService.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(WebComponentConstants.MessageTypes.OPENAUTH, @"{""screen"":""account""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHCLOSE, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSKIPPROJECT, null)]
        [DataRow(WebComponentConstants.MessageTypes.HIDESIGNINBANNER, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSIGNIN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENBROWSERAGAIN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSIGNOUT, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHRETRY, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSELECTACCOUNT, @"{""accountId"":""acc-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHLINKPROJECT, @"{""projectId"":""proj-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN, @"{""token"":""tok-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHREMOVEACCESSTOKEN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENSERVERSETTING, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENPATPAGE, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHREAUTHORIZE, null)]
        public async Task EveryAuthType_WhileInactive_IsRoutedAndDoesNothing(string messageType, string payloadJson)
        {
            var authService = new Mock<IAuthService>();
            authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive);
            var logger = new FakeLogger();
            var router = new AuthMessageRouter(authService.Object, logger);

            var payload = payloadJson != null ? JToken.Parse(payloadJson) : null;
            var routed = await router.TryRouteAsync(messageType, payload);

            Assert.IsTrue(routed);
            authService.Verify(x => x.Current, Times.Once);
            authService.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(WebComponentConstants.MessageTypes.OPENAUTH, @"{""screen"":""account""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHCLOSE, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSKIPPROJECT, null)]
        [DataRow(WebComponentConstants.MessageTypes.HIDESIGNINBANNER, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSIGNIN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENBROWSERAGAIN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSIGNOUT, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHRETRY, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSELECTACCOUNT, @"{""accountId"":""acc-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHLINKPROJECT, @"{""projectId"":""proj-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN, @"{""token"":""tok-123""}")]
        [DataRow(WebComponentConstants.MessageTypes.AUTHREMOVEACCESSTOKEN, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENSERVERSETTING, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHOPENPATPAGE, null)]
        [DataRow(WebComponentConstants.MessageTypes.AUTHREAUTHORIZE, null)]
        public async Task EveryAuthType_WhileActive_CallsItsAction(string messageType, string payloadJson)
        {
            var authService = new Mock<IAuthService>();
            authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive.WithActive(true));
            authService.Setup(x => x.OpenAuth(It.IsAny<string>()));
            authService.Setup(x => x.CloseAuthAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.SkipProject());
            authService.Setup(x => x.HideSignInBanner());
            authService.Setup(x => x.SignInAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.OpenBrowserAgainAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.SignOutAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.RetryAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.SelectAccountAsync(It.IsAny<JToken>())).Returns(Task.CompletedTask);
            authService.Setup(x => x.LinkProjectAsync(It.IsAny<JToken>())).Returns(Task.CompletedTask);
            authService.Setup(x => x.SetAccessTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            authService.Setup(x => x.RemoveAccessTokenAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.OpenServerSettingAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.OpenPatPageAsync()).Returns(Task.CompletedTask);
            authService.Setup(x => x.ReauthorizeAsync()).Returns(Task.CompletedTask);

            var logger = new FakeLogger();
            var router = new AuthMessageRouter(authService.Object, logger);

            var payload = payloadJson != null ? JToken.Parse(payloadJson) : null;
            var routed = await router.TryRouteAsync(messageType, payload);

            Assert.IsTrue(routed);

            authService.Verify(ExpectedCalls[messageType], Times.Once);
        }

        [TestMethod]
        public async Task LinkProjectWithNull_PassesANullToken()
        {
            var authService = new Mock<IAuthService>();
            authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive.WithActive(true));
            authService.Setup(x => x.LinkProjectAsync(It.IsAny<JToken>())).Returns(Task.CompletedTask);

            var logger = new FakeLogger();
            var router = new AuthMessageRouter(authService.Object, logger);

            var payload = JToken.Parse(@"{""projectId"":null}");
            var routed = await router.TryRouteAsync(WebComponentConstants.MessageTypes.AUTHLINKPROJECT, payload);

            Assert.IsTrue(routed);
            authService.Verify(x => x.LinkProjectAsync(It.Is<JToken>(t => t != null && t.Type == JTokenType.Null)), Times.Once);
        }

        [TestMethod]
        public async Task FailingAction_LogsOnlyTypeAndCode()
        {
            var authService = new Mock<IAuthService>();
            authService.SetupGet(x => x.Current).Returns(AuthSnapshot.Inactive.WithActive(true));
            authService.Setup(x => x.SetAccessTokenAsync(It.IsAny<string>()))
                .ThrowsAsync(new DevtoolsException("Test error", -32803, "trace-id", "auth/test-error"));

            var logger = new FakeLogger();
            var router = new AuthMessageRouter(authService.Object, logger);

            var payload = JToken.Parse(@"{""token"":""secret-token-123""}");
            var routed = await router.TryRouteAsync(WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN, payload);

            Assert.IsTrue(routed);
            Assert.AreEqual(1, logger.Warnings.Count);
            StringAssert.Contains(logger.Warnings[0], "[auth]");
            StringAssert.Contains(logger.Warnings[0], "auth-set-access-token");
            StringAssert.Contains(logger.Warnings[0], "failed");
            StringAssert.Contains(logger.Warnings[0], "code=auth/test-error");
            Assert.IsFalse(logger.Warnings[0].Contains("secret-token-123"));
        }
    }
}
