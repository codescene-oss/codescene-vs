using Core.Models;
using Core.Models.ReviewResult;
using Core.Models.ReviewResultModel;
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
        public IEnumerable<ReviewModel> Map(ReviewResultModel result)
        {
            var list = new List<ReviewModel>();

            foreach (var item in result.FunctionLevelCodeSmells)
            {
                foreach (var smell in item.CodeSmells)
                {
                    list.Add(Map(result.RawScore?.Name, smell));
                }
            }
            foreach (var item in result.ExpressionLevelCodeSmells)
            {
                list.Add(Map(result.RawScore?.Name, item));
            }
            return list;
        }
        private ReviewModel Map(string path, CodeSmellModel review)
        {
            return new ReviewModel
            {
                Path = path,
                Category = review.Category,
                Details = review.Details,
                StartLine = review.Range.Startline,
                EndLine = review.Range.EndLine,
                StartColumn = review.Range.StartColumn,
                EndColumn = review.Range.EndColumn
            };
        }
        private ReviewModel Map(string path, ExpressionLevelCodeSmellModel review)
        {
            return new ReviewModel
            {
                Path = path,
                Category = review.Category,
                Details = review.Details,
                StartLine = review.Range.Startline,
                EndLine = review.Range.EndLine,
                StartColumn = review.Range.StartColumn,
                EndColumn = review.Range.EndColumn
            };
        }
    }
}