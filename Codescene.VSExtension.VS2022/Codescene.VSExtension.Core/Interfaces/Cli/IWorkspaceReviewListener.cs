// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IWorkspaceReviewListener
    {
        event EventHandler<Application.Cli.Rpc.FileReviewAppliedEventArgs> ReviewApplied;

        event EventHandler<Application.Cli.Rpc.DeltaReviewAppliedEventArgs> DeltaApplied;

        event EventHandler<Application.Cli.Rpc.ReviewFailedEventArgs> ReviewFailed;

        void Attach(IIdeServerClient client);

        void Detach();

        string SubmitBufferReview(string repoRoot, string relPath, string absolutePath, string content, string baselineRevision = null);

        void SubmitDiskReviews(string repoRoot, IReadOnlyList<string> relPaths, string baselineRevision = null);
    }
}
