// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    [Export(typeof(IAuthMessageRouter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class AuthMessageRouter : IAuthMessageRouter
    {
        private readonly IAuthService _authService;
        private readonly ILogger _logger;
        private readonly Dictionary<string, Func<JToken, Task>> _handlers;

        [ImportingConstructor]
        public AuthMessageRouter(IAuthService authService, ILogger logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _handlers = new Dictionary<string, Func<JToken, Task>>
            {
                [WebComponentConstants.MessageTypes.OPENAUTH] = HandleOpenAuthAsync,
                [WebComponentConstants.MessageTypes.AUTHCLOSE] = HandleAuthCloseAsync,
                [WebComponentConstants.MessageTypes.AUTHSKIPPROJECT] = HandleSkipProjectAsync,
                [WebComponentConstants.MessageTypes.HIDESIGNINBANNER] = HandleHideSignInBannerAsync,
                [WebComponentConstants.MessageTypes.AUTHSIGNIN] = HandleSignInAsync,
                [WebComponentConstants.MessageTypes.AUTHOPENBROWSERAGAIN] = HandleOpenBrowserAgainAsync,
                [WebComponentConstants.MessageTypes.AUTHSIGNOUT] = HandleSignOutAsync,
                [WebComponentConstants.MessageTypes.AUTHRETRY] = HandleRetryAsync,
                [WebComponentConstants.MessageTypes.AUTHSELECTACCOUNT] = HandleSelectAccountAsync,
                [WebComponentConstants.MessageTypes.AUTHLINKPROJECT] = HandleLinkProjectAsync,
                [WebComponentConstants.MessageTypes.AUTHSETACCESSTOKEN] = HandleSetAccessTokenAsync,
                [WebComponentConstants.MessageTypes.AUTHREMOVEACCESSTOKEN] = HandleRemoveAccessTokenAsync,
                [WebComponentConstants.MessageTypes.AUTHOPENSERVERSETTING] = HandleOpenServerSettingAsync,
                [WebComponentConstants.MessageTypes.AUTHOPENPATPAGE] = HandleOpenPatPageAsync,
                [WebComponentConstants.MessageTypes.AUTHREAUTHORIZE] = HandleReauthorizeAsync,
            };
        }

        public async Task<bool> TryRouteAsync(string messageType, JToken payload)
        {
            if (messageType == null || !_handlers.TryGetValue(messageType, out var handler))
            {
                return false;
            }

            if (!_authService.Current.Active)
            {
                return true;
            }

            try
            {
                await handler(payload).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex) ?? "unknown";
                _logger.Warn($"[auth] {messageType} failed code={code}");
            }

            return true;
        }

        private Task HandleOpenAuthAsync(JToken payload)
        {
            var screen = payload?.ToObject<OpenAuthPayload>()?.Screen;
            _authService.OpenAuth(screen);
            return Task.CompletedTask;
        }

        private Task HandleAuthCloseAsync(JToken payload)
        {
            return _authService.CloseAuthAsync();
        }

        private Task HandleSkipProjectAsync(JToken payload)
        {
            _authService.SkipProject();
            return Task.CompletedTask;
        }

        private Task HandleHideSignInBannerAsync(JToken payload)
        {
            _authService.HideSignInBanner();
            return Task.CompletedTask;
        }

        private Task HandleSignInAsync(JToken payload)
        {
            return _authService.SignInAsync();
        }

        private Task HandleOpenBrowserAgainAsync(JToken payload)
        {
            return _authService.OpenBrowserAgainAsync();
        }

        private Task HandleSignOutAsync(JToken payload)
        {
            return _authService.SignOutAsync();
        }

        private Task HandleRetryAsync(JToken payload)
        {
            return _authService.RetryAsync();
        }

        private Task HandleSelectAccountAsync(JToken payload)
        {
            var accountId = payload?.ToObject<AuthSelectAccountPayload>()?.AccountId;
            return _authService.SelectAccountAsync(accountId);
        }

        private Task HandleLinkProjectAsync(JToken payload)
        {
            var projectId = payload?.ToObject<AuthLinkProjectPayload>()?.ProjectId;
            return _authService.LinkProjectAsync(projectId);
        }

        private Task HandleSetAccessTokenAsync(JToken payload)
        {
            var token = payload?.ToObject<AuthSetAccessTokenPayload>()?.Token;
            return _authService.SetAccessTokenAsync(token);
        }

        private Task HandleRemoveAccessTokenAsync(JToken payload)
        {
            return _authService.RemoveAccessTokenAsync();
        }

        private Task HandleOpenServerSettingAsync(JToken payload)
        {
            return _authService.OpenServerSettingAsync();
        }

        private Task HandleOpenPatPageAsync(JToken payload)
        {
            return _authService.OpenPatPageAsync();
        }

        private Task HandleReauthorizeAsync(JToken payload)
        {
            return _authService.ReauthorizeAsync();
        }
    }
}
