using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
        FileReviewModel Review(string path);
        void UseFileOnPathType();
        void UseContentOnlyType(string content);
    }
}
