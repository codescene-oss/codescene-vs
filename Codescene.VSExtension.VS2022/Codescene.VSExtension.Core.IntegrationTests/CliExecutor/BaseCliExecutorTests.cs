using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    public abstract class BaseCliExecutorTests : BaseIntegrationTests
    {
        protected ICliExecutor cliExecutor;
        protected string tempCacheDir;


        public new virtual void Initialize()
        {
            base.Initialize();
            cliExecutor = GetService<ICliExecutor>();

            tempCacheDir = Path.Combine(Path.GetTempPath(), "codescene-test-cache", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempCacheDir);

            mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns(tempCacheDir);
        }

        public virtual void Cleanup()
        {
            if (Directory.Exists(tempCacheDir))
            {
                try
                {
                    Directory.Delete(tempCacheDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
