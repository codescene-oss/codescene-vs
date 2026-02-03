using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    public abstract class BaseCliExecutorTests : BaseIntegrationTests
    {
        protected ICliExecutor CliExecutor;
        protected string TempCacheDir;


        public new virtual void Initialize()
        {
            base.Initialize();
            CliExecutor = GetService<ICliExecutor>();

            TempCacheDir = Path.Combine(Path.GetTempPath(), "codescene-test-cache", Guid.NewGuid().ToString());
            Directory.CreateDirectory(TempCacheDir);

            MockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns(TempCacheDir);
        }

        public virtual void Cleanup()
        {
            if (Directory.Exists(TempCacheDir))
            {
                try
                {
                    Directory.Delete(TempCacheDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
