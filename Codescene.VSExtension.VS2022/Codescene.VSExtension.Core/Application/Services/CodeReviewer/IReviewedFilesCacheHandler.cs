using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface IReviewedFilesCacheHandler
    {
        void Add(FileReviewModel model);
        FileReviewModel Get(string path);
        bool Remove(string path);
        bool Exists(string path);
        void Add(CachedRefactoringActionModel model);
        CachedRefactoringActionModel GetRefactored();
        void ClearRefactored();
    }
}
