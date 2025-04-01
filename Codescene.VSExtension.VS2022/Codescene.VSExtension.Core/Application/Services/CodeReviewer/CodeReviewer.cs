using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecuter _executer;

        [Import]
        private readonly IReviewedFilesCacheHandler _cache;

        public FileReviewModel Review(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (_cache.Exists(path))
            {
                return _cache.Get(path);
            }

            var review = _executer.Review(path);
            var mapped = _mapper.Map(path, review);
            _cache.Add(mapped);
            return mapped;
        }

        public FileReviewModel ReviewContent(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentNullException(nameof(content));
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var review = _executer.ReviewContent(fileName, content);
            var mapped = _mapper.Map(path, review);
            _cache.Add(mapped);
            return mapped;
        }
    }
}
