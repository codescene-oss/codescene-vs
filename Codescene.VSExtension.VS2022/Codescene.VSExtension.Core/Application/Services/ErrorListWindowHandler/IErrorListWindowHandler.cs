using Codescene.VSExtension.Core.Models.ReviewResultModel;

namespace Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler
{
    public interface IErrorListWindowHandler
    {
        //string GetUrl();
        //void Handle(IEnumerable<IssueModel> issues);
        //void Handle(string filePath, CsReview review);
        void Handle(string filePath, ReviewMapModel review);
    }
}
