using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;
using CodesceneReeinventTest.Models;
using System.Collections.Generic;
using System.Linq;

namespace CodesceneReeinventTest.Application;
internal class ModelMapper : IModelMapper
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

    private ReviewModel Map(string path, CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult.Review review)
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