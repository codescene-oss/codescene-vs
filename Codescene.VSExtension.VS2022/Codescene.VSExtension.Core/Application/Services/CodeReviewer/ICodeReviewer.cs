using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
        void InvalidateCache(string path);
        FileReviewModel Review(string path, bool invalidateCache = false);
        void UseFileOnPathType();
        void UseContentOnlyType(string content);
        List<CodeSmellModel> GetCodesmellExpressions(string path, bool invalidateCache = false);
        Task<RefactorResponseModel> Refactor(string path, string content, bool invalidateCache = false);
        CachedRefactoringActionModel GetCachedRefactoredCode();
        Task<RefactorResponseModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false);
        void AddPathInCache(string path);
        string GetCachedPath();
    }
}
