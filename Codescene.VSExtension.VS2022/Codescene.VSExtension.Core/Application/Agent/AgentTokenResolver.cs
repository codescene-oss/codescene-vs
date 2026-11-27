// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal class AgentTokenResolver
    {
        private readonly ISettingsProvider _settings;
        private readonly AuthSnapshot _snapshot;
        private readonly IIdeServerAuthClient _authClient;
        private readonly ILogger _logger;

        public AgentTokenResolver(
            ISettingsProvider settings,
            AuthSnapshot snapshot,
            IIdeServerAuthClient authClient,
            ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> ResolveAsync(CancellationToken cancellationToken)
        {
            var pat = _settings.AuthToken?.Trim();
            if (!string.IsNullOrEmpty(pat))
            {
                return pat;
            }

            if (!_snapshot.Active || _snapshot.Status == null)
            {
                return null;
            }

            var state = _snapshot.Status["state"]?.ToString();
            if (state != "signed-in")
            {
                return null;
            }

            try
            {
                var result = await _authClient.AuthTokenAsync(cancellationToken);
                var accessToken = result?["access-token"]?.ToString();
                return accessToken;
            }
            catch (Exception ex)
            {
                var redactedMessage = SensitiveDataRedactor.RedactSecrets(ex.Message);
                _logger.Debug($"Failed to retrieve auth token: {redactedMessage}");
                return null;
            }
        }
    }
}
