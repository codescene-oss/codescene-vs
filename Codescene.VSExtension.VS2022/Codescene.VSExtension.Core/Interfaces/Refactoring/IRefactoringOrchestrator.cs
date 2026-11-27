// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;

namespace Codescene.VSExtension.Core.Interfaces.Refactoring
{
    public interface IRefactoringOrchestrator
    {
        RefactoringSession Current { get; }

        Task RequestAsync(string source, string path, FnToRefactorModel fn);

        void Cancel();

        void MarkStale();

        void UpdateRange(CliRangeModel range);

        void ReportUserAction(string stage);
    }
}
