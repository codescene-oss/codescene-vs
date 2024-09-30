using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;
using CodesceneReeinventTest.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest.Application;
internal interface IModelMapper
{
    IEnumerable<ReviewModel> Map(CsReview result);
}
