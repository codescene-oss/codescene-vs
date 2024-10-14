using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Core.Application.Services.IssueHandler;
using Core.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest;

[Command(PackageIds.OpenErrorListWindowCommand)]
internal sealed class OpenErrorListWindowCommand(DIToolkitPackage package, IIssuesHandler handler) : BaseDICommand(package)
{
    private readonly IIssuesHandler _issuesHandler = handler;

    protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
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

        _issuesHandler.Handle(issues);

        return Task.CompletedTask;
    }
}
