// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICodeReviewer
    {
        Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, CancellationToken cancellationToken = default);
    }
}
