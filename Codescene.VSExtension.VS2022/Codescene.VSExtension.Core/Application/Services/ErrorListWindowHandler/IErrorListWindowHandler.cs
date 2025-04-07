using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler
{
    public interface IErrorListWindowHandler
    {
        void Handle(FileReviewModel review);
    }
}
