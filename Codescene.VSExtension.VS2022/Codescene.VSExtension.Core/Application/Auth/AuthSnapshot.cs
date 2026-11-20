// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public sealed class AuthSnapshot : IEquatable<AuthSnapshot>
    {
        public AuthSnapshot(
            bool active,
            JObject status,
            JArray accounts,
            JArray projects,
            AuthRepo repo,
            JObject linkedProject,
            string projectSource,
            string projectReason,
            JArray projectCandidates,
            string surface,
            string step,
            string context,
            bool syncing,
            bool signInBannerHidden)
        {
            Active = active;
            Status = status;
            Accounts = accounts;
            Projects = projects;
            Repo = repo;
            LinkedProject = linkedProject;
            ProjectSource = projectSource;
            ProjectReason = projectReason;
            ProjectCandidates = projectCandidates ?? new JArray();
            Surface = surface;
            Step = step;
            Context = context;
            Syncing = syncing;
            SignInBannerHidden = signInBannerHidden;
        }

        public static AuthSnapshot Inactive { get; } = new AuthSnapshot(
            false,
            null,
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

        public bool Active { get; }

        public JObject Status { get; }

        public JArray Accounts { get; }

        public JArray Projects { get; }

        public AuthRepo Repo { get; }

        public JObject LinkedProject { get; }

        public string ProjectSource { get; }

        public string ProjectReason { get; }

        public JArray ProjectCandidates { get; }

        public string Surface { get; }

        public string Step { get; }

        public string Context { get; }

        public bool Syncing { get; }

        public bool SignInBannerHidden { get; }

        public AuthSnapshot WithActive(bool active)
        {
            return new AuthSnapshot(active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithStatus(JObject status)
        {
            return new AuthSnapshot(Active, status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithAccounts(JArray accounts)
        {
            return new AuthSnapshot(Active, Status, accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithProjects(JArray projects)
        {
            return new AuthSnapshot(Active, Status, Accounts, projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithRepo(AuthRepo repo)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithLinkedProject(JObject linkedProject)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, linkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithProjectSource(string projectSource)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, projectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithProjectReason(string projectReason)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, projectReason, ProjectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithProjectCandidates(JArray projectCandidates)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, projectCandidates, Surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithSurface(string surface)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, surface, Step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithStep(string step)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, step, Context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithContext(string context)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, context, Syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithSyncing(bool syncing)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, syncing, SignInBannerHidden);
        }

        public AuthSnapshot WithSignInBannerHidden(bool signInBannerHidden)
        {
            return new AuthSnapshot(Active, Status, Accounts, Projects, Repo, LinkedProject, ProjectSource, ProjectReason, ProjectCandidates, Surface, Step, Context, Syncing, signInBannerHidden);
        }

        public bool Equals(AuthSnapshot other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(Repo, other.Repo) && ScalarsEqual(other) && JsonEqual(other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AuthSnapshot);
        }

        public override int GetHashCode()
        {
            var parts = new object[] { Active, Repo, ProjectSource, ProjectReason, Surface, Step, Context, Syncing, SignInBannerHidden };
            return parts.Aggregate(17, (hash, part) => unchecked((hash * 397) ^ (part?.GetHashCode() ?? 0)));
        }

        private bool ScalarsEqual(AuthSnapshot other)
        {
            var mine = new object[] { Active, ProjectSource, ProjectReason, Surface, Step, Context, Syncing, SignInBannerHidden };
            var theirs = new object[] { other.Active, other.ProjectSource, other.ProjectReason, other.Surface, other.Step, other.Context, other.Syncing, other.SignInBannerHidden };
            return mine.SequenceEqual(theirs);
        }

        private bool JsonEqual(AuthSnapshot other)
        {
            var mine = new JToken[] { Status, Accounts, Projects, LinkedProject, ProjectCandidates };
            var theirs = new JToken[] { other.Status, other.Accounts, other.Projects, other.LinkedProject, other.ProjectCandidates };
            return mine.Zip(theirs, JToken.DeepEquals).All(equal => equal);
        }
    }
}
