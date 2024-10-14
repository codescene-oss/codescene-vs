using Core.Models;
using Core.Models.ReviewResult;
using System.Collections.Generic;

namespace Core.Application.Services.Mapper
{
    public interface IModelMapper
    {
        IEnumerable<ReviewModel> Map(CsReview result);
    }
}
