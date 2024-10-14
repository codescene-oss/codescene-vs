using CodeLensShared;
using CodesceneReeinventTest.Models;
using System.Collections.Generic;

namespace CodesceneReeinventTest.Application;
internal interface IModelMapper
{
    IEnumerable<ReviewModel> Map(CsReview result);
}
