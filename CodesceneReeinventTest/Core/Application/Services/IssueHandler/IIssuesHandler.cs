using Core.Models;
using Core.Models.ReviewResult;
using Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Core.Application.Services.IssueHandler
{
    public interface IIssuesHandler
    {
        string GetUrl();
        void Handle(IEnumerable<IssueModel> issues);
        void Handle(string filePath, CsReview review);
        void Handle(string filePath, ReviewMapModel review);
    }
}
