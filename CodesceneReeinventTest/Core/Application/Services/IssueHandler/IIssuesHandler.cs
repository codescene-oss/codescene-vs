using Core.Models;
using Core.Models.ReviewResult;
using System.Collections.Generic;

namespace Core.Application.Services.IssueHandler
{
    public interface IIssuesHandler
    {
        string GetUrl();
        void Handle(IEnumerable<IssueModel> issues);
        void Handle(string filePath, CsReview review);
    }
}
