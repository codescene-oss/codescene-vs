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

        //[Import]
        //private readonly IReviewedFilesCacheHandler _cache;

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

        public FileReviewModel Review(string path)
        {
            if (_type == ReviewType.CONTENT_ONLY && string.IsNullOrWhiteSpace(_content))
            {
                throw new ArgumentNullException(nameof(_content));
            }

            return _type == ReviewType.FILE_ON_PATH ? ReviewFileOnPath(path) : ReviewContent(path, _content);
        }

        private FileReviewModel ReviewFileOnPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            //if (_cache.Exists(path))
            //{
            //    return _cache.Get(path);
            //}

            var review = _executer.Review(path);
            var mapped = _mapper.Map(path, review);
            //_cache.Add(mapped);
            return mapped;
        }

        private FileReviewModel ReviewContent(string path, string content)
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
            return mapped;
        }
    }
}
