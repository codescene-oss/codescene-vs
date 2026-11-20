// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerAuthClient
    {
        event EventHandler<JObject> AuthStatusChanged;

        Task<JObject> AuthConfigureAsync(string serverUrl, string accessToken, CancellationToken cancellationToken = default);

        Task<JObject> AuthStatusAsync(CancellationToken cancellationToken = default);

        Task<JObject> AuthLoginAsync(CancellationToken cancellationToken = default);

        Task<JObject> AuthReauthorizeAsync(CancellationToken cancellationToken = default);

        Task<JObject> AuthLogoutAsync(CancellationToken cancellationToken = default);

        Task<JObject> AuthCancelLoginAsync(string loginId, CancellationToken cancellationToken = default);

        Task<JArray> AuthListAccountsAsync(CancellationToken cancellationToken = default);

        Task<JObject> AuthSelectAccountAsync(JToken accountId, CancellationToken cancellationToken = default);

        Task<JObject> AuthSyncAccountAsync(string repoPath, CancellationToken cancellationToken = default);

        Task<JArray> ProjectsListAsync(CancellationToken cancellationToken = default);

        Task<JObject> ProjectLinkGetAsync(string repoPath, CancellationToken cancellationToken = default);

        Task<JObject> ProjectLinkSetAsync(string repoPath, JToken projectId, CancellationToken cancellationToken = default);
    }
}
