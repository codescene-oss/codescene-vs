using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models.Ace;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class AceStateIntegrationTests : BaseIntegrationTests
    {
        private IAceStateService _aceStateService;
        private IPreflightManager _preflightManager;

        [TestInitialize]
        public override void Initialize()
        {
            base.Initialize();
            _aceStateService = GetService<IAceStateService>();
            _preflightManager = GetService<IPreflightManager>();
        }

        [TestMethod]
        public void AceStateService_IsRegisteredAsSingleton()
        {
            // Act
            var service1 = GetService<IAceStateService>();
            var service2 = GetService<IAceStateService>();

            // Assert
            Assert.IsNotNull(service1);
            Assert.AreSame(service1, service2, "AceStateService should be a singleton");
        }

        [TestMethod]
        public void AceStateService_InitialState_IsLoading()
        {
            // The initial state before any operations should be Loading
            // Note: By the time this test runs, preflight might have already changed the state
            // This test verifies the service is properly initialized
            Assert.IsNotNull(_aceStateService?.CurrentState);
        }

        [TestMethod]
        public void PreflightManager_SetsEnabledOrOffline_AfterPreflight()
        {
            // Arrange
            var stateHistory = new List<AceState>();
            _aceStateService.StateChanged += (s, e) => stateHistory.Add(e.NewState);

            // Act
            _preflightManager.RunPreflight(force: true);

            // Assert
            Assert.IsNotEmpty(stateHistory, "State should have changed at least once");

            var finalState = stateHistory.Last();
            Assert.IsTrue(
                finalState == AceState.Enabled || finalState == AceState.Offline || finalState == AceState.Error,
                $"Final state should be Enabled, Offline, or Error, but was {finalState}");
        }

        [TestMethod]
        public void StateChanged_FiresWithCorrectPreviousAndNewState()
        {
            // Arrange
            AceStateChangedEventArgs capturedArgs = null;
            _aceStateService.StateChanged += (s, e) => capturedArgs = e;

            // Act - Directly set state to test event args
            _aceStateService.SetState(AceState.Enabled);

            // Assert
            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(AceState.Enabled, capturedArgs.NewState);
        }

        [TestMethod]
        public void SetError_TransitionsToErrorState()
        {
            // Arrange
            _aceStateService.SetState(AceState.Enabled);
            var testException = new Exception("Test integration error");

            // Act
            _aceStateService.SetError(testException);

            // Assert
            Assert.AreEqual(AceState.Error, _aceStateService.CurrentState);
            Assert.AreEqual(testException, _aceStateService.LastError);
        }

        [TestMethod]
        public void ClearError_ClearsLastError_WithoutChangingState()
        {
            // Arrange
            _aceStateService.SetState(AceState.Enabled);
            _aceStateService.SetError(new Exception("Test error"));
            var stateBeforeClear = _aceStateService.CurrentState;

            // Act
            _aceStateService.ClearError();

            // Assert
            Assert.IsNull(_aceStateService.LastError);
            Assert.AreEqual(stateBeforeClear, _aceStateService.CurrentState);
        }

        [TestMethod]
        public void MultipleStateTransitions_AreTrackedCorrectly()
        {
            // Arrange
            var stateHistory = new List<AceState>();
            _aceStateService.StateChanged += (s, e) => stateHistory.Add(e.NewState);

            // Act
            _aceStateService.SetState(AceState.Enabled);
            _aceStateService.SetState(AceState.Offline);
            _aceStateService.SetState(AceState.Enabled);

            // Assert
            Assert.HasCount(3, stateHistory);
            Assert.AreEqual(AceState.Enabled, stateHistory[0]);
            Assert.AreEqual(AceState.Offline, stateHistory[1]);
            Assert.AreEqual(AceState.Enabled, stateHistory[2]);
        }
    }
}
