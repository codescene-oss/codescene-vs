using Codescene.VSExtension.Core.Models.ReviewModels;
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

        public void Add(FileReviewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _reviews[model.FilePath] = model;
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
