// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Auth;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AuthSnapshotTests
    {
        private static ContractBundle _bundle = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestMethod]
        public void AccountDataKey_ForSignedIn_JoinsServerSourceUserAccount()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);

            var key = AuthStatusFields.AccountDataKey(status);

            Assert.IsNotNull(key);
            StringAssert.Contains(key, "https://cloud.example.invalid");
            StringAssert.Contains(key, "oauth");
            StringAssert.Contains(key, "1234");
            StringAssert.Contains(key, "42");
            var parts = key.Split('|');
            Assert.AreEqual(4, parts.Length);
        }

        [TestMethod]
        public void AccountDataKey_ForNotSignedIn_IsNull()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-out", FixtureForm.Wire);

            var key = AuthStatusFields.AccountDataKey(status);

            Assert.IsNull(key);
        }

        [TestMethod]
        public void AccountDataKey_WithNullAccount_StillProducesAKey()
        {
            var status = (JObject)_bundle.Fixture("auth-status", "signed-in-setting-token-onprem", FixtureForm.Wire);

            var key = AuthStatusFields.AccountDataKey(status);

            Assert.IsNotNull(key);
            StringAssert.Contains(key, "https://onprem.example.invalid");
            StringAssert.Contains(key, "setting-token");
            StringAssert.Contains(key, "17");
            var parts = key.Split('|');
            Assert.AreEqual(4, parts.Length);
        }

        [TestMethod]
        public void Equals_WithDeepEqualJson_IsTrue()
        {
            var status1 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var status2 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var accounts1 = new JArray(new JObject { ["id"] = "1", ["name"] = "Account 1" });
            var accounts2 = new JArray(new JObject { ["id"] = "1", ["name"] = "Account 1" });
            var snapshot1 = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status1)
                .WithAccounts(accounts1);
            var snapshot2 = AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(status2)
                .WithAccounts(accounts2);

            var equal = snapshot1.Equals(snapshot2);

            Assert.IsTrue(equal);
        }

        [TestMethod]
        public void Equals_WithADifferentNestedValue_IsFalse()
        {
            var status1 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            var status2 = (JObject)_bundle.Fixture("auth-status", "signed-in", FixtureForm.Wire);
            status2["user"]["name"] = "Different User";
            var snapshot1 = AuthSnapshot.Inactive.WithActive(true).WithStatus(status1);
            var snapshot2 = AuthSnapshot.Inactive.WithActive(true).WithStatus(status2);

            var equal = snapshot1.Equals(snapshot2);

            Assert.IsFalse(equal);
        }
    }
}
