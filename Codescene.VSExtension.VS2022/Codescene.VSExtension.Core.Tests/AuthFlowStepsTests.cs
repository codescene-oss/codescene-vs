// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthFlowStepsTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        [DataRow(AuthConstants.FlowKinds.IDLE, "signed-out")]
        [DataRow(AuthConstants.FlowKinds.IDLE, "signing-in")]
        [DataRow(AuthConstants.FlowKinds.SIGNIN, "signed-out")]
        [DataRow(AuthConstants.FlowKinds.SIGNIN, "signing-in")]
        [DataRow(AuthConstants.FlowKinds.MANUALACCOUNT, "signed-out")]
        [DataRow(AuthConstants.FlowKinds.MANUALACCOUNT, "signing-in")]
        [DataRow(AuthConstants.FlowKinds.MANUALPROJECT, "signed-out")]
        [DataRow(AuthConstants.FlowKinds.MANUALPROJECT, "signing-in")]
        public void Next_WhenIdleOrNotSignedIn_IsSummary(string flowKind, string statusVariant)
        {
            var status = (JObject)_bundle.Fixture("auth-status", statusVariant, FixtureForm.Wire);
            var flow = new AuthFlowState(flowKind, false, false, false);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthConstants.Steps.SUMMARY, step);
        }

        [TestMethod]
        public void Next_ManualAccount_IsPickAccountUntilChosenThenDone()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var flow = new AuthFlowState(AuthConstants.FlowKinds.MANUALACCOUNT, false, false, false);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var stepBeforeChosen = AuthFlowSteps.Next(flow, snapshot);
            Assert.AreEqual(AuthConstants.Steps.PICKACCOUNT, stepBeforeChosen);

            var flowAfterChosen = flow.WithAccountChosen(true);
            var stepAfterChosen = AuthFlowSteps.Next(flowAfterChosen, snapshot);
            Assert.AreEqual(AuthFlowSteps.DONE, stepAfterChosen);
        }

        [TestMethod]
        public void Next_ManualProject_IsPickProject()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var flow = new AuthFlowState(AuthConstants.FlowKinds.MANUALPROJECT, false, false, false);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, step);
        }

        [TestMethod]
        public void Next_SignInUnresolved_IsResolving()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, false, false, false);
            var snapshot = AuthSnapshot.Inactive.WithActive(true).WithStatus(status);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthConstants.Steps.RESOLVING, step);
        }

        [TestMethod]
        public void Next_SignInResolvedWithSeveralAccountsAndNoLink_IsPickAccount()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = new JArray(
                new JObject { ["id"] = "1", ["name"] = "Account 1" },
                new JObject { ["id"] = "2", ["name"] = "Account 2" });
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(accounts);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthConstants.Steps.PICKACCOUNT, step);
        }

        [TestMethod]
        public void Next_SignInResolvedWithALinkedProject_SkipsAccountPick()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = new JArray(
                new JObject { ["id"] = "1", ["name"] = "Account 1" },
                new JObject { ["id"] = "2", ["name"] = "Account 2" });
            var linkedProject = new JObject { ["id"] = "123", ["name"] = "My Project" };
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(accounts)
                .WithLinkedProject(linkedProject);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthFlowSteps.DONE, step);
        }

        [TestMethod]
        public void Next_SignInResolvedWhenAccountChosen_SkipsAccountPick()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts = new JArray(
                new JObject { ["id"] = "1", ["name"] = "Account 1" },
                new JObject { ["id"] = "2", ["name"] = "Account 2" });
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, true, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithAccounts(accounts);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthFlowSteps.DONE, step);
        }

        [TestMethod]
        public void Next_SignInWithRepoProjectsAndNoLink_IsPickProject()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var repo = new AuthRepo("/path/to/repo", "repo");
            var projects = new JArray(new JObject { ["id"] = "123", ["name"] = "Project 1" });
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithRepo(repo)
                .WithProjects(projects);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthConstants.Steps.PICKPROJECT, step);
        }

        [TestMethod]
        public void Next_SignInWithoutRepo_IsDone()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var projects = new JArray(new JObject { ["id"] = "123", ["name"] = "Project 1" });
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithProjects(projects);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthFlowSteps.DONE, step);
        }

        [TestMethod]
        public void Next_SignInWithEmptyProjects_IsDone()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var repo = new AuthRepo("/path/to/repo", "repo");
            var projects = new JArray();
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, false);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithRepo(repo)
                .WithProjects(projects);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthFlowSteps.DONE, step);
        }

        [TestMethod]
        public void Next_SignInWhenSkipped_IsDone()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var repo = new AuthRepo("/path/to/repo", "repo");
            var projects = new JArray(new JObject { ["id"] = "123", ["name"] = "Project 1" });
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, true);
            var snapshot = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status)
                .WithRepo(repo)
                .WithProjects(projects);

            var step = AuthFlowSteps.Next(flow, snapshot);

            Assert.AreEqual(AuthFlowSteps.DONE, step);
        }
    }
}
