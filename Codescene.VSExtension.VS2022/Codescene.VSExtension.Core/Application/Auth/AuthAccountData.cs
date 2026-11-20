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
            var accountsTask = FetchAsync(() => client.AuthListAccountsAsync(), logger);
            var projectsTask = FetchAsync(() => client.ProjectsListAsync(), logger);
            var linkTask = repo != null ? FetchAsync(() => client.ProjectLinkGetAsync(repo.Path), logger) : Task.FromResult<JObject>(null);

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

        private static async Task<T> FetchAsync<T>(Func<Task<T>> request, ILogger logger)
            where T : class
        {
            try
            {
                return await request().ConfigureAwait(false);
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
