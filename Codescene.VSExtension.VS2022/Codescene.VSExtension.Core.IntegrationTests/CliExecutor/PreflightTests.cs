namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    [TestClass]
    public class PreflightTests: BaseCliExecutorTests
    {
        [TestInitialize]
        public override void Initialize() => base.Initialize();

        [TestCleanup]
        public override void Cleanup() => base.Cleanup();

        [TestMethod]
        public void Preflight_ReturnsFileTypes()
        {
            // Act
            var result = CliExecutor.Preflight(force: true);

            // Assert
            Assert.IsNotNull(result, "CLI should return a preflight response");
            Assert.IsNotNull(result.FileTypes, "Preflight should include file types");
            Assert.IsTrue(result.FileTypes.Length > 0, "Preflight should return at least one supported file type");

            var fileTypes = result.FileTypes.Select(ft => ft.ToLower()).ToArray();
            Assert.IsTrue(fileTypes.Any(ft => ft.Contains("cs") || ft.Contains("csharp")),
                "C# should be a supported file type");
        }
    }
}
