using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
        FileReviewModel Review(string path, string content);
        DeltaResponseModel Delta(FileReviewModel review, string currentCode);
    }
}
