// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthAccountData
    {
        public static async Task<(JArray Accounts, JArray Projects, JObject Link)> FetchAsync(
            IIdeServerAuthClient client,
            AuthRepo repo,
            ILogger logger)
        {
            var accountsTask = FetchAccountsAsync(client, logger);
            var projectsTask = FetchProjectsAsync(client, logger);
            var linkTask = repo != null ? FetchLinkAsync(client, repo, logger) : Task.FromResult<JObject>(null);

            await Task.WhenAll(accountsTask, projectsTask, linkTask).ConfigureAwait(false);

            return (await accountsTask.ConfigureAwait(false),
                    await projectsTask.ConfigureAwait(false),
                    await linkTask.ConfigureAwait(false));
        }

        public static (JObject LinkedProject, string ProjectSource, string ProjectReason, JArray ProjectCandidates) LinkFields(JObject link)
        {
            if (link == null)
            {
                return (null, null, null, new JArray());
            }

            var project = link["project"] as JObject;
            if (project != null && project.Type != JTokenType.Null)
            {
                var source = link["source"]?.Value<string>();
                return (project, source, null, new JArray());
            }

            var reason = link["reason"]?.Value<string>();
            var candidates = link["candidates"] as JArray ?? new JArray();
            return (null, null, reason, candidates);
        }

        private static async Task<JArray> FetchAccountsAsync(IIdeServerAuthClient client, ILogger logger)
        {
            try
            {
                return await client.AuthListAccountsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex) ?? "unknown";
                logger.Warn($"[auth] account data request failed code={code}");
                return null;
            }
        }

        private static async Task<JArray> FetchProjectsAsync(IIdeServerAuthClient client, ILogger logger)
        {
            try
            {
                return await client.ProjectsListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex) ?? "unknown";
                logger.Warn($"[auth] account data request failed code={code}");
                return null;
            }
        }

        private static async Task<JObject> FetchLinkAsync(IIdeServerAuthClient client, AuthRepo repo, ILogger logger)
        {
            try
            {
                return await client.ProjectLinkGetAsync(repo.Path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex) ?? "unknown";
                logger.Warn($"[auth] account data request failed code={code}");
                return null;
            }
        }
    }
}
