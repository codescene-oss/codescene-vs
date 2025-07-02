using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecutor
    {
        CliReviewModel ReviewContent(string filename, string content);
        string GetFileVersion();
        string GetDeviceId();
    }
}
