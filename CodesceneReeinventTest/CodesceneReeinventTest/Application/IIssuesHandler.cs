using CodesceneReeinventTest.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest.Application;
internal interface IIssuesHandler
{
    string GetUrl();
    void Handle(IEnumerable<IssueModel> issues);
}
