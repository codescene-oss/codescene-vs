using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICodeReviewer
    {
        FileReviewModel Review(string path, string content);

        DeltaResponseModel Delta(FileReviewModel review, string currentCode);
    }
}
