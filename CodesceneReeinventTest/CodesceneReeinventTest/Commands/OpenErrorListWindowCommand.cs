
using Core.Application.Services.IssueHandler;
using Core.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest;

internal sealed class OpenErrorListWindowCommand(IIssuesHandler issuesHandler) : VsCommandBase
{
    internal const int Id = PackageIds.OpenErrorListWindowCommand;

    protected override async void InvokeInternal()
    {
        var issues = new List<IssueModel> {
             new() {
                    Code = new CodeModel
                    {
                        Target = new TargetModel
                        {
                            Mid = 1,
                            Path = "codescene.openInteractiveDocsPanel",
                            Scheme = "command",
                            Query = ""
                        },
                        Value = "Bumpy Road Ahead"
                    },
                    Resource = "/c:/Users/Amina/FitnessApp/ApplicationCore/Helpers/Error/ErrorHandlerHelper.cs",
                    Owner = "codescene",
                    Severity = 4,
                    Message = "Bumpy Road Ahead (bumps = 3)",
                    Source = "CodeScene",
                    StartLineNumber = 12,
                    StartColumn = 27,
                    EndLineNumber = 12,
                    EndColumn = 43
                }};

        issuesHandler.Handle(issues);
    }
}
