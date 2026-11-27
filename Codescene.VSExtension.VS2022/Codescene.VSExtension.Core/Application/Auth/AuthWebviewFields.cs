// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthWebviewFields
    {
        public static JObject ToAuthPayload(AuthSnapshot snapshot)
        {
            if (!snapshot.Active || snapshot.Status == null)
            {
                return null;
            }

            var status = (JObject)snapshot.Status.DeepClone();

            var repo = snapshot.Repo != null
                ? new JObject { ["path"] = snapshot.Repo.Path, ["name"] = snapshot.Repo.Name }
                : null;

            return new JObject
            {
                ["status"] = status,
                ["accounts"] = snapshot.Accounts,
                ["projects"] = snapshot.Projects,
                ["repo"] = repo,
                ["linked-project"] = snapshot.LinkedProject,
            };
        }

        public static JObject ToAuthFlow(AuthSnapshot snapshot)
        {
            if (!snapshot.Active)
            {
                return null;
            }

            return new JObject
            {
                ["step"] = snapshot.Step,
                ["context"] = snapshot.Context,
                ["project-source"] = snapshot.ProjectSource,
                ["syncing"] = snapshot.Syncing,
                ["project-reason"] = snapshot.ProjectReason,
                ["project-candidates"] = snapshot.ProjectCandidates,
            };
        }

        public static void Inject(JObject payload, AuthSnapshot snapshot)
        {
            var authPayload = ToAuthPayload(snapshot);
            if (authPayload == null)
            {
                return;
            }

            var featureFlags = payload["featureFlags"] as JArray;
            if (featureFlags == null)
            {
                featureFlags = new JArray();
                payload["featureFlags"] = featureFlags;
            }

            if (!featureFlags.Any(flag => flag.Value<string>() == "ide-auth"))
            {
                featureFlags.Add("ide-auth");
            }

            payload["auth"] = authPayload;
            payload["authFlow"] = ToAuthFlow(snapshot);

            if (snapshot.SignInBannerHidden)
            {
                payload["signInBannerHidden"] = true;
            }
        }
    }
}
