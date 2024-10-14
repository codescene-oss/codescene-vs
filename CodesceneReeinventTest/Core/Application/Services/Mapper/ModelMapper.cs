using Core.Models;
using Core.Models.ReviewResult;
using System.Collections.Generic;
using System.Linq;

namespace Core.Application.Services.Mapper
{
    public class ModelMapper : IModelMapper
    {
        public IEnumerable<ReviewModel> Map(CsReview result)
        {
            var list = new List<ReviewModel>();

            foreach (var item in result.Review)
            {
                list.Add(Map(result.RawScore?.Name, item));
            }

            return list;
        }

        private ReviewModel Map(string path, Review review)
        {
            return new ReviewModel
            {
                Path = path,
                Category = review.Category,
                Details = review.Functions.First().Details,
                StartLine = review.Functions.First().Startline,
                EndLine = review.Functions.First().Endline,
            };
        }

    }
}