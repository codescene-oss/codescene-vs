using Codescene.VSExtension.Core.Application.Authentication;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FakeAuthDataProviderTests
    {
        private FakeAuthDataProvider _provider;

        [TestInitialize]
        public void Setup()
        {
            _provider = new FakeAuthDataProvider();
        }

        [TestMethod]
        public void GetData_ReturnsLoginResponse()
        {
            var result = _provider.GetData();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetData_ReturnsExpectedToken()
        {
            var result = _provider.GetData();

            Assert.AreEqual("token", result.Token);
        }

        [TestMethod]
        public void GetData_ReturnsExpectedName()
        {
            var result = _provider.GetData();

            Assert.AreEqual("amina@reeinvent.com", result.Name);
        }

        [TestMethod]
        public void GetData_ReturnsExpectedUserId()
        {
            var result = _provider.GetData();

            Assert.AreEqual("1234", result.UserId);
        }

        [TestMethod]
        public void Clear_DoesNotThrow()
        {
            // Clear should complete without throwing
            _provider.Clear();
        }

        [TestMethod]
        public void Store_DoesNotThrow()
        {
            var loginResponse = new Models.LoginResponse
            {
                Token = "new-token",
                Name = "new-user",
                UserId = "5678",
            };

            // Store should complete without throwing
            _provider.Store(loginResponse);
        }

        [TestMethod]
        public void Store_DoesNotAffectGetData()
        {
            // Arrange
            var newResponse = new Models.LoginResponse
            {
                Token = "new-token",
                Name = "new-user",
                UserId = "5678",
            };

            // Act
            _provider.Store(newResponse);
            var result = _provider.GetData();

            // Assert - FakeAuthDataProvider always returns the same fake data
            Assert.AreEqual("token", result.Token);
            Assert.AreEqual("amina@reeinvent.com", result.Name);
        }

        [TestMethod]
        public void Clear_DoesNotAffectGetData()
        {
            // Act
            _provider.Clear();
            var result = _provider.GetData();

            // Assert - FakeAuthDataProvider always returns the same fake data
            Assert.IsNotNull(result);
            Assert.AreEqual("token", result.Token);
        }
    }
}
