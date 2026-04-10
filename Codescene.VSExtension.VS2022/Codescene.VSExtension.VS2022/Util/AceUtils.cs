// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;

namespace Codescene.VSExtension.VS2022.Util
{
    public static class AceUtils
    {
        public static FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine));
        }

        public static async Task<FnToRefactorModel> GetRefactorableFunctionDeltaAsync(GetRefactorableFunctionsModel model)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflight = await preflightManager.GetPreflightResponseAsync();

            if (model.FunctionRange == null)
            {
                return null;
            }

            var fileContent = await GetFileContentAsync(model);
            var fileName = Path.GetFileName(model.Path);
            var deltaCache = new DeltaCacheService();
            var cache = deltaCache.GetAll();

            if (cache.TryGetValue(model.Path, out var delta) && delta != null)
            {
                var refactorableFunctions = await aceManager.GetRefactorableFunctionsFromDeltaAsync(fileName, fileContent, delta, preflight);
                return refactorableFunctions?.FirstOrDefault(x => x.Name == model.FunctionName);
            }

            return null;
        }

        public static async Task<FnToRefactorModel> GetRefactorableFunctionCodeSmellAsync(GetRefactorableFunctionsModel model)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflight = await preflightManager.GetPreflightResponseAsync();

            if (model.FunctionRange == null)
            {
                return null;
            }

            var codeSmell = new CliCodeSmellModel()
            {
                Details = model.Details,
                Category = model.Category,
                Range = new Core.Models.Cli.CliRangeModel()
                {
                    StartColumn = model.FunctionRange.StartColumn,
                    EndColumn = model.FunctionRange.EndColumn,
                    StartLine = model.FunctionRange.StartLine,
                    EndLine = model.FunctionRange.EndLine,
                },
            };

            var fileContent = await GetFileContentAsync(model);

            var refactorableFunctions = await aceManager.GetRefactorableFunctionsFromCodeSmellsAsync(model.Path, fileContent, new List<CliCodeSmellModel> { codeSmell }, preflight);
            return refactorableFunctions?.FirstOrDefault();
        }

        private static async Task<string> GetFileContentAsync(GetRefactorableFunctionsModel model)
        {
            var fileContent = string.Empty;
            var docView = await VS.Documents.OpenAsync(model.Path);
            if (docView?.TextBuffer is ITextBuffer buffer)
            {
                fileContent = buffer.CurrentSnapshot.GetText();
            }

            return fileContent;
        }
    }
}
