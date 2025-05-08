using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(IReviewedFilesCacheHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ReviewedFilesCacheHandler : IReviewedFilesCacheHandler
    {

        private static readonly Dictionary<string, FileReviewModel> _reviews = new Dictionary<string, FileReviewModel>();
        private static CachedRefactoringActionModel _refactored = null;

        public void Add(FileReviewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _reviews[model.FilePath] = model;
        }

        public void Add(CachedRefactoringActionModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _refactored = model;
        }

        public void ClearRefactored()
        {
            _refactored = null;
        }

        public bool Exists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            return _reviews.TryGetValue(path, out _);
        }

        public FileReviewModel Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            var exists = _reviews.TryGetValue(path, out FileReviewModel model);

            if (!exists)
            {
                throw new Exception($"Missing review for the path:{path}");
            }

            return model;
        }

        public CachedRefactoringActionModel GetRefactored()
        {
            if (_refactored == null)
            {
                throw new Exception($"{nameof(_refactored)} is null");
            }

            return _refactored;
        }

        public bool Remove(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            return _reviews.Remove(path);
        }
    }
}
