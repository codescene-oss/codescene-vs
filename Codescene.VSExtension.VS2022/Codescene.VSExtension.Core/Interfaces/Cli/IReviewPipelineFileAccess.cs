// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IReviewPipelineFileAccess
    {
        ReviewDocument FindOpenDocument(string filePath);

        Task<ReviewDocument> OpenDocumentAsync(string filePath);

        Task<byte[]> ReadFileBytesAsync(string filePath);

        bool IsVisible(string filePath);
    }
}
