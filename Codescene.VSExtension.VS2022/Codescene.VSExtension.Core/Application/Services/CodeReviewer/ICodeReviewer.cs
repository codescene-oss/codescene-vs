﻿using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
        FileReviewModel Review(string path, string content);
        Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false);
        CachedRefactoringActionModel GetCachedRefactoredCode();
        Task<RefactorResponseModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false);
    }
}
