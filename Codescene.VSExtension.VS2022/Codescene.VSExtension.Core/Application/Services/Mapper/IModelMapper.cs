using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.Mapper
{
    public interface IModelMapper
    {
        //IEnumerable<ReviewModel> Map(CsReview result);
        //IEnumerable<ReviewModel> MapToList(CliReviewModel result);
        FileReviewModel Map(string filePath, CliReviewModel result);
    }
}
