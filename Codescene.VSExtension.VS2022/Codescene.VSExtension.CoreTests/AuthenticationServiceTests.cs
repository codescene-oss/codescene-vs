using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class AuthenticationServiceTests
    {
        private Mock<IPersistenceAuthDataProvider> _mockPersistenceProvider;
        private AuthenticationService _authService;

        [TestInitialize]
        public void Setup()
        {
            _mockPersistenceProvider = new Mock<IPersistenceAuthDataProvider>();
            _authService = new AuthenticationService(_mockPersistenceProvider.Object);
        }

        #region IsLoggedIn Tests

        [TestMethod]
        public void IsLoggedIn_WhenNoDataInMemoryAndProviderReturnsNull_ReturnsFalse()
        {
            // Arrange
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns((LoginResponse)null);

            // Act
            var result = _authService.IsLoggedIn();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLoggedIn_WhenNoDataInMemoryAndProviderReturnsData_ReturnsTrue()
        {
            // Arrange
            var loginResponse = new LoginResponse
            {
                Token = "test-token",
                Name = "TestUser",
                UserId = "123"
            };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);

            // Act
            var result = _authService.IsLoggedIn();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsLoggedIn_CachesResultFromProvider()
        {
            // Arrange
            var loginResponse = new LoginResponse
            {
                Token = "test-token",
                Name = "TestUser",
                UserId = "123"
            };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);

            // Act - call twice
            _authService.IsLoggedIn();
            _authService.IsLoggedIn();

            // Assert - provider should only be called once due to caching
            _mockPersistenceProvider.Verify(x => x.GetData(), Times.Once);
        }

        [TestMethod]
        public void IsLoggedIn_WhenAlreadyCached_DoesNotCallProvider()
        {
            // Arrange
            var loginResponse = new LoginResponse
            {
                Token = "test-token",
                Name = "TestUser",
                UserId = "123"
            };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);

            // Act - first call loads from provider
            _authService.IsLoggedIn();
            
            // Reset mock to track new calls
            _mockPersistenceProvider.Invocations.Clear();
            
            // Second call should use cache
            var result = _authService.IsLoggedIn();

            // Assert
            Assert.IsTrue(result);
            _mockPersistenceProvider.Verify(x => x.GetData(), Times.Never);
        }

        #endregion

        #region GetData Tests

        [TestMethod]
        public void GetData_WhenNotLoggedIn_ReturnsNull()
        {
            // Arrange
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns((LoginResponse)null);

            // Act
            var result = _authService.GetData();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetData_AfterIsLoggedInCachesData_ReturnsData()
        {
            // Arrange
            var loginResponse = new LoginResponse
            {
                Token = "cached-token",
                Name = "CachedUser",
                UserId = "456"
            };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);

            // Act - first login to cache data
            _authService.IsLoggedIn();
            var result = _authService.GetData();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("cached-token", result.Token);
            Assert.AreEqual("CachedUser", result.Name);
            Assert.AreEqual("456", result.UserId);
        }

        #endregion

        #region SignOut Tests

        [TestMethod]
        public void SignOut_ClearsInMemoryData()
        {
            // Arrange
            var loginResponse = new LoginResponse
            {
                Token = "test-token",
                Name = "TestUser",
                UserId = "123"
            };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);
            _authService.IsLoggedIn(); // Cache the data

            // Act
            _authService.SignOut();

            // Assert - GetData should return null after sign out
            var result = _authService.GetData();
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SignOut_CallsPersistenceProviderClear()
        {
            // Arrange
            var loginResponse = new LoginResponse { Token = "token", Name = "user", UserId = "1" };
            _mockPersistenceProvider.Setup(x => x.GetData()).Returns(loginResponse);
            _authService.IsLoggedIn();

            // Act
            _authService.SignOut();

            // Assert
            _mockPersistenceProvider.Verify(x => x.Clear(), Times.Once);
        }

        [TestMethod]
        public void SignOut_FiresOnSignedOutEvent()
        {
            // Arrange
            var eventFired = false;
            _authService.OnSignedOut += () => eventFired = true;

            // Act
            _authService.SignOut();

            // Assert
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void SignOut_WhenNotLoggedIn_StillCallsClearAndFiresEvent()
        {
            // Arrange
            var eventFired = false;
            _authService.OnSignedOut += () => eventFired = true;

            // Act
            _authService.SignOut();

            // Assert
            _mockPersistenceProvider.Verify(x => x.Clear(), Times.Once);
            Assert.IsTrue(eventFired);
        }

        #endregion
    }
}
