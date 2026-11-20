// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthValueEqualityTests
    {
        public static IEnumerable<object[]> DistinctValuePairs()
        {
            var flow = new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, false, false, false);
            yield return new object[] { flow, flow.WithKind(AuthConstants.FlowKinds.MANUALACCOUNT) };
            yield return new object[] { flow, flow.WithResolved(true) };
            yield return new object[] { flow, flow.WithAccountChosen(true) };
            yield return new object[] { flow, flow.WithProjectSkipped(true) };

            var repo = new AuthRepo("/repo", "repo");
            yield return new object[] { repo, new AuthRepo("/other", "repo") };
            yield return new object[] { repo, new AuthRepo("/repo", "other") };

            var snapshot = SignedInSnapshot();
            yield return new object[] { snapshot, snapshot.WithActive(false) };
            yield return new object[] { snapshot, snapshot.WithRepo(new AuthRepo("/other", "other")) };
            yield return new object[] { snapshot, snapshot.WithStatus(new JObject { ["state"] = "signed-out" }) };
            yield return new object[] { snapshot, snapshot.WithAccounts(new JArray()) };
            yield return new object[] { snapshot, snapshot.WithSurface(AuthConstants.Surfaces.AUTH) };
            yield return new object[] { snapshot, snapshot.WithSignInBannerHidden(true) };
        }

        public static IEnumerable<object[]> EqualValueCopies()
        {
            yield return new object[] { new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, true), new AuthFlowState(AuthConstants.FlowKinds.SIGNIN, true, false, true) };
            yield return new object[] { new AuthRepo("/repo", "repo"), new AuthRepo("/repo", "repo") };
            yield return new object[] { new AuthRepo(null, null), new AuthRepo(null, null) };
            yield return new object[] { SignedInSnapshot(), SignedInSnapshot() };
        }

        [TestMethod]
        [DynamicData(nameof(EqualValueCopies))]
        public void EqualCopies_AreEqualAndHashAlike(object first, object second)
        {
            Assert.AreNotSame(first, second);
            Assert.IsTrue(first.Equals(second));
            Assert.IsTrue(first.Equals((object)second));
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        [DynamicData(nameof(DistinctValuePairs))]
        public void DifferingValues_AreNotEqual(object first, object second)
        {
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(second.Equals(first));
        }

        [TestMethod]
        [DynamicData(nameof(EqualValueCopies))]
        public void Values_EqualThemselvesAndNeverNullOrOtherTypes(object value, object unused)
        {
            Assert.IsTrue(value.Equals(value));
            Assert.IsFalse(value.Equals(null));
            Assert.IsFalse(value.Equals("not a value"));
        }

        [TestMethod]
        public void TypedEquals_WithNull_IsFalse()
        {
            Assert.IsFalse(AuthFlowState.Idle.Equals((AuthFlowState)null));
            Assert.IsFalse(new AuthRepo("/repo", "repo").Equals((AuthRepo)null));
            Assert.IsFalse(AuthSnapshot.Inactive.Equals((AuthSnapshot)null));
        }

        private static AuthSnapshot SignedInSnapshot()
        {
            return AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(new JObject { ["state"] = "signed-in", ["user"] = new JObject { ["id"] = "u1" } })
                .WithAccounts(new JArray(new JObject { ["id"] = "a1" }))
                .WithRepo(new AuthRepo("/repo", "repo"))
                .WithProjectSource("remote");
        }
    }
}
