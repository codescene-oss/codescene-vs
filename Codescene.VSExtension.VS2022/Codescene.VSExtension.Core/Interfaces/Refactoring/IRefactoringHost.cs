// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Interfaces.Refactoring
{
    public interface IRefactoringHost
    {
        bool IsDirty(string path);

        Task<bool> ConfirmSaveAsync(string fileName);

        Task SaveAsync(string path);

        Task OfferSetApiKeyAsync(string message);

        Task ShowReviewAsync(AceComponentData data);

        Task UpdateReviewAsync(AceComponentData data);

        string ReadDocumentText(string path);
    }
}
