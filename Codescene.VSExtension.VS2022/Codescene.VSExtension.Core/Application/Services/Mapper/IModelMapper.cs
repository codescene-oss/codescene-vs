using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResult;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Mapper
{
    public interface IModelMapper
    {
        IEnumerable<ReviewModel> Map(CsReview result);
        IEnumerable<ReviewModel> MapToList(ReviewResultModel result);
        ReviewMapModel Map(ReviewResultModel result);
    }
}
