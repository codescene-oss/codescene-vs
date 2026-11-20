// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerLogAuthTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        [DataRow("cs-ide/auth-configure")]
        [DataRow("cs-ide/auth-cancel-login")]
        [DataRow("cs-ide/project-link-get")]
        [DataRow("cs-ide/project-link-set")]
        public void RequestSummary_ForANonObjectArgument_IsEmpty(string method)
        {
            Assert.AreEqual(string.Empty, IdeServerLog.SummarizeRequest(method, "not an object"));
        }

        [TestMethod]
        [DataRow("cs-ide/auth-status")]
        [DataRow("cs-ide/auth-list-accounts")]
        [DataRow("cs-ide/project-link-get")]
        public void ResponseSummary_ForAnUnexpectedResult_IsEmpty(string method)
        {
            Assert.AreEqual(string.Empty, IdeServerLog.SummarizeResponse(method, "not json"));
        }

        [TestMethod]
        public void AuthConfigureSummary_RecordsPresenceOnly_NeverTheValues()
        {
            var fixture = (JObject)_bundle.Fixture("auth-configure-params", "full", FixtureForm.Wire);
            var serverUrl = (string?)fixture["server-url"];
            var accessToken = (string?)fixture["access-token"];

            var summary = IdeServerLog.SummarizeRequest("cs-ide/auth-configure", fixture);

            Assert.IsFalse(summary.Contains(serverUrl!), "Server URL must not appear in the log");
            Assert.IsFalse(summary.Contains(accessToken!), "Access token must not appear in the log");
            Assert.IsTrue(summary.Contains("serverUrl="), "Summary must record serverUrl presence");
            Assert.IsTrue(summary.Contains("tokenPresent="), "Summary must record token presence");
        }

        [TestMethod]
        public void AuthStatusSummary_RecordsStateAndCredentialSource()
        {
            var fixture = (JObject)_bundle.Fixture("auth-status", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeResponse("cs-ide/auth-status", fixture);

            Assert.IsTrue(summary.Contains("state="), "Summary must include state");
            Assert.IsTrue(summary.Contains("credentialSource="), "Summary must include credentialSource");
        }

        [TestMethod]
        public void LoginPendingSummary_RecordsLoginPendingFlag()
        {
            var fixture = (JObject)_bundle.Fixture("login-pending", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeResponse("cs-ide/auth-login", fixture);

            Assert.IsTrue(summary.Contains("loginPending=True"), "Summary must include loginPending flag");
        }

        [TestMethod]
        public void AccountListSummary_RecordsCount()
        {
            var fixture = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeResponse("cs-ide/auth-list-accounts", fixture);

            Assert.IsTrue(summary.Contains("count="), "Summary must include count");
        }

        [TestMethod]
        public void ProjectListSummary_RecordsCount()
        {
            var fixture = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeResponse("cs-ide/projects-list", fixture);

            Assert.IsTrue(summary.Contains("count="), "Summary must include count");
        }

        [TestMethod]
        public void ProjectLinkSummary_RecordsLinkedSourceAndReason()
        {
            var linkedFixture = (JObject)_bundle.Fixture("project-link", "remote", FixtureForm.Wire);

            var linkedSummary = IdeServerLog.SummarizeResponse("cs-ide/project-link-get", linkedFixture);

            Assert.IsTrue(linkedSummary.Contains("linked="), "Summary must include linked status");
            Assert.IsTrue(linkedSummary.Contains("source="), "Summary must include source");

            var notLinkedFixture = (JObject)_bundle.Fixture("project-link", "no-project-candidates", FixtureForm.Wire);

            var notLinkedSummary = IdeServerLog.SummarizeResponse("cs-ide/project-link-get", notLinkedFixture);

            Assert.IsTrue(notLinkedSummary.Contains("linked="), "Summary must include linked status");
            Assert.IsTrue(notLinkedSummary.Contains("reason="), "Summary must include reason when not linked");
        }

        [TestMethod]
        public void RepoPathRequestSummary_RecordsRepoPathPresence()
        {
            var fixture = (JObject)_bundle.Fixture("repo-path-params", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeRequest("cs-ide/auth-sync-account", fixture);

            Assert.IsTrue(summary.Contains("repo="), "Summary must include repo");
        }

        [TestMethod]
        public void CancelLoginRequestSummary_RecordsLoginIdPresence()
        {
            var fixture = (JObject)_bundle.Fixture("cancel-login-params", "full", FixtureForm.Wire);

            var summary = IdeServerLog.SummarizeRequest("cs-ide/auth-cancel-login", fixture);

            Assert.IsTrue(summary.Contains("loginId="), "Summary must include loginId presence");
            Assert.IsFalse(summary.Contains((string)fixture["login-id"]), "The actual login ID must not appear in the log");
        }

        [TestMethod]
        public void SummariesForEveryAuthMethod_ContainNoIdsOrNames()
        {
            var authStatus = (JObject)_bundle.Fixture("auth-status", "full", FixtureForm.Wire);
            var accountList = (JArray)_bundle.Fixture("account-list", "full", FixtureForm.Wire);
            var projectList = (JArray)_bundle.Fixture("project-list", "full", FixtureForm.Wire);
            var projectLink = (JObject)_bundle.Fixture("project-link", "full", FixtureForm.Wire);
            var loginPending = (JObject)_bundle.Fixture("login-pending", "full", FixtureForm.Wire);

            var forbiddenStrings = new[] { authStatus["user"], authStatus["account"], projectLink["project"] }
                .Concat(accountList)
                .Concat(projectList)
                .OfType<JObject>()
                .SelectMany(item => new[] { item["id"], item["name"] })
                .Append(authStatus["server"]?["url"])
                .Where(token => token != null)
                .Select(token => token.ToString())
                .ToList();

            Assert.IsTrue(forbiddenStrings.Count > 0, "The fixtures must provide ids and names to check against");

            var summaries = new List<string>
            {
                IdeServerLog.SummarizeResponse("cs-ide/auth-status", authStatus),
                IdeServerLog.SummarizeResponse("cs-ide/auth-login", loginPending),
                IdeServerLog.SummarizeResponse("cs-ide/auth-list-accounts", accountList),
                IdeServerLog.SummarizeResponse("cs-ide/projects-list", projectList),
                IdeServerLog.SummarizeResponse("cs-ide/project-link-get", projectLink),
                IdeServerLog.SummarizeResponse("cs-ide/project-link-set", projectLink),
            };

            foreach (var (forbidden, summary) in forbiddenStrings.SelectMany(f => summaries.Select(s => (f, s))))
            {
                Assert.IsFalse(summary.Contains(forbidden), $"Summary must not contain the ID or name: {forbidden}");
            }
        }
    }
}
