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

        //public void AddToActiveReviewList(string documentPath, ReviewMapModel review)
        //{
        //    var review = Review(documentPath);
        //    ActiveReviewList.Add(documentPath, review);
        //}

        //public void AddToActiveReviewList(string documentPath, string content)
        //{
        //    var review = ReviewContent(documentPath, content);
        //    ActiveReviewList[documentPath] = review;
        //}

        //public void RemoveFromActiveReviewList(string documentPath)
        //{
        //    ActiveReviewList.Remove(documentPath);
        //}

        //public ReviewMapModel GetReviewObject(string filePath)
        //{
        //    ActiveReviewList.TryGetValue(filePath, out var review);

        //    //for already opened files on IDE load
        //    if (review == null)
        //    {
        //        AddToActiveReviewList(filePath);
        //        ActiveReviewList.TryGetValue(filePath, out review);
        //    }
        //    return review;
        //}

        //public List<ReviewModel> GetTaggerItems(string filePath)
        //{
        //    var review = GetReviewObject(filePath);
        //    return review.ExpressionLevel.Concat(review.FunctionLevel).ToList();
        //}
    }
}
