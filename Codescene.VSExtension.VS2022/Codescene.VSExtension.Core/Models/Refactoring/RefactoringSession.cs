// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.Refactoring
{
    public class RefactoringSession
    {
        public RefactoringSession(string path, FnToRefactorModel function, string source)
        {
            TaskId = "refactor-" + Guid.NewGuid().ToString("N");
            Path = path;
            Function = function;
            Source = source;
            State = RefactoringSessionState.Running;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public string TaskId { get; }

        public string Path { get; }

        public FnToRefactorModel Function { get; private set; }

        public string Source { get; }

        public RefactoringSessionState State { get; private set; }

        public string ProgressMessage { get; private set; }

        public RefactorResponseModel Response { get; private set; }

        public string Error { get; private set; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public void SetProgress(string message)
        {
            ProgressMessage = message;
        }

        public void SetResponse(RefactorResponseModel response)
        {
            Response = response;
            State = RefactoringSessionState.Presented;
        }

        public void SetError(string error)
        {
            Error = error;
            State = RefactoringSessionState.Failed;
        }

        public void Cancel()
        {
            State = RefactoringSessionState.Cancelled;
            CancellationTokenSource?.Cancel();
        }

        public void MarkStale()
        {
            State = RefactoringSessionState.Stale;
        }

        public void UpdateRange(CliRangeModel newRange)
        {
            if (Function != null)
            {
                Function = new FnToRefactorModel
                {
                    Name = Function.Name,
                    Body = Function.Body,
                    FileType = Function.FileType,
                    FunctionType = Function.FunctionType,
                    Range = newRange,
                    RefactoringTargets = Function.RefactoringTargets,
                };
            }
        }
    }
}
