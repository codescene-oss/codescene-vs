using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    public interface IAceManager
    {
        Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false);
        Task<CachedRefactoringActionModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false);
        CachedRefactoringActionModel GetCachedRefactoredCode();
        Task<IList<FnToRefactorModel>> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension);
    }
}