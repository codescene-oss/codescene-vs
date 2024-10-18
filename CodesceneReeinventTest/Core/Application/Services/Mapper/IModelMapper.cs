using Core.Models;
using Core.Models.ReviewResult;
using Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Core.Application.Services.Mapper
{
    public interface IModelMapper
    {
        IEnumerable<ReviewModel> Map(CsReview result);
        IEnumerable<ReviewModel> MapToList(ReviewResultModel result);
        ReviewMapModel Map(ReviewResultModel result);
    }
}
