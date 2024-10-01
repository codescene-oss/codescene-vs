using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;
using CodesceneReeinventTest.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest.Application;
internal interface IIssuesHandler
{
    string GetUrl();
    void Handle(IEnumerable<IssueModel> issues);
    void Handle(string filePath, CsReview review);
}
