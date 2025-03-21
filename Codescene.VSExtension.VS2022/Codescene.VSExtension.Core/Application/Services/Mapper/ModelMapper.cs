using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResult;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.Mapper
{
    [Export(typeof(IModelMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
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
        public IEnumerable<ReviewModel> MapToList(ReviewResultModel result)
        {
            var list = new List<ReviewModel>();

            foreach (var item in result.FunctionLevelCodeSmells)
            {
                foreach (var smell in item.CodeSmells)
                {
                    //list.Add(Map(result.RawScore?.Name, smell));
                }
            }
            //foreach (var item in result.ExpressionLevelCodeSmells)
            //{
            //    list.Add(Map(result.RawScore?.Name, item));
            //}
            return list;
        }
        public ReviewMapModel Map(ReviewResultModel result)
        {
            return new ReviewMapModel
            {
                Score = result.Score ?? 0,
                //ExpressionLevel = result.ExpressionLevelCodeSmells.Select(x => Map(x)).ToList(),
                FileLevel = result.FileLevelCodeSmells.Select(x => Map(x)).ToList(),
                FunctionLevel = result.FunctionLevelCodeSmells.SelectMany(x => x.CodeSmells.Select(y => Map(y))).ToList()
            };
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
        private ReviewModel Map(CodeSmellModel review)
        {
            return new ReviewModel
            {
                Category = review.Category,
                Details = review.Details,
                StartLine = review.Range.Startline - 1,
                EndLine = review.Range.EndLine - 1,
                StartColumn = review.Range.StartColumn - 1,
                EndColumn = review.Range.EndColumn - 1
            };
        }
    }
}