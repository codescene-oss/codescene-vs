// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(OnClickRefactoringHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnClickRefactoringHandler
{
    [Import]
    private readonly IRefactoringOrchestrator _orchestrator;

    public string Path { get; private set; }

    public FnToRefactorModel RefactorableFunction { get; private set; }

    public async Task HandleAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        Path = path;
        RefactorableFunction = refactorableFunction;

        await _orchestrator.RequestAsync(entryPoint, path, refactorableFunction);
    }

    public void HandleCancel()
    {
        _orchestrator.Cancel();
    }
}
