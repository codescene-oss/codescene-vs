using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResult;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.IssueHandler
{
    public interface IIssuesHandler
    {
        string GetUrl();
        void Handle(IEnumerable<IssueModel> issues);
        void Handle(string filePath, CsReview review);
        void Handle(string filePath, ReviewMapModel review);
    }
}
