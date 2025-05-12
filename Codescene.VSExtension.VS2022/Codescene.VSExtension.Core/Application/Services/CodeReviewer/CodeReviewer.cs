using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        private string _content = string.Empty;
        private enum ReviewType
        {
            FILE_ON_PATH,
            CONTENT_ONLY
        }

        private ReviewType _type = ReviewType.FILE_ON_PATH;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecuter _executer;

        [Import]
        private readonly IReviewedFilesCacheHandler _cache;

        public void UseFileOnPathType()
        {
            _type = ReviewType.FILE_ON_PATH;
            _content = string.Empty;
        }

        public void UseContentOnlyType(string content)
        {
            _type = ReviewType.CONTENT_ONLY;
            _content = content;
        }

        public List<CodeSmellModel> GetCodesmellExpressions(string path, bool invalidateCache = false)
        {
            var review = Review(path, invalidateCache);
            return review.FunctionLevel;
        }

        public FileReviewModel Review(string path, bool invalidateCache = false)
        {
            if (_type == ReviewType.CONTENT_ONLY && string.IsNullOrWhiteSpace(_content))
            {
                throw new ArgumentNullException(nameof(_content));
            }

            ValidatePath(path);

            if (invalidateCache)
            {
                InvalidateCache(path);
            }

            if (_cache.Exists(path))
            {
                return _cache.Get(path);
            }

            var review = _type == ReviewType.FILE_ON_PATH ? _executer.Review(path) : ReviewFileContent(path, _content);

            var mapped = _mapper.Map(path, review);

            _cache.Add(mapped);

            return mapped;
        }

        private void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
        }

        private void ValidateContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentNullException(nameof(content));
            }
        }

        private string GetFileName(string path)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return fileName;
        }

        private CliReviewModel ReviewFileContent(string path, string content)
        {
            ValidateContent(content);

            var fileName = GetFileName(path);

            var review = _executer.ReviewContent(fileName, content);

            return review;
        }

        public void InvalidateCache(string path)
        {
            _cache.Remove(path);
        }

        public async Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false)
        {
            ValidatePath(path);

            UseContentOnlyType(content);

            var review = ReviewFileContent(path, content);

            var codesmellsJson = JsonConvert.SerializeObject(review.FunctionLevelCodeSmells[0].CodeSmells);

            var preflight = JsonConvert.SerializeObject(_executer.Preflight());

            var fileName = GetFileName(path);

            var extension = Path.GetExtension(fileName).Replace(".", "");

            var refactorableFunctions = await _executer.FnsToRefactorFromCodeSmellsAsync(content, extension, codesmellsJson, preflight);

            var f = refactorableFunctions.First();

            //Fix for csharp ACE api
            if (string.IsNullOrWhiteSpace(f.FunctionType))
            {
                f.FunctionType = "MemberFn";
            }

            var refactorableFunctionsString = JsonConvert.SerializeObject(f);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: true);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = f,
                Refactored = refactoredFunctions
            };

            _cache.Add(cacheItem);

            return cacheItem;
        }

        public async Task<RefactorResponseModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(refactorableFunction.FunctionType))
            {
                refactorableFunction.FunctionType = "MemberFn";
            }

            var refactorableFunctionsString = JsonConvert.SerializeObject(refactorableFunction);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: true);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = refactorableFunction,
                Refactored = refactoredFunctions
            };

            _cache.Add(cacheItem);

            return refactoredFunctions;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return _cache.GetRefactored();
        }
    }
}
