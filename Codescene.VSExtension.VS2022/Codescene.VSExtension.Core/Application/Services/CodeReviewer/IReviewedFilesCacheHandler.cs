using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface IReviewedFilesCacheHandler
    {
        void Add(FileReviewModel model);
        FileReviewModel Get(string path);
        bool Remove(string path);
        bool Exists(string path);
        void Add(RefactorResponseModel model);
        RefactorResponseModel GetRefactored();
        void ClearRefactored();
    }
}
