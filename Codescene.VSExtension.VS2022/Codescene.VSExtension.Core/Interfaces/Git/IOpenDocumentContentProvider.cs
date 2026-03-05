// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IOpenDocumentContentProvider
    {
        Task<string> GetContentForReviewAsync(string filePath);
    }
}
