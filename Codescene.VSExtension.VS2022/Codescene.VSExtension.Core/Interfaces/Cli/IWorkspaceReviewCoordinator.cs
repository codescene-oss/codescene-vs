// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IWorkspaceReviewCoordinator
    {
        Task StartAsync(string solutionPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default);

        void Stop();

        void SubmitBufferReview(string absolutePath, string content);
    }
}
