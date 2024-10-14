using Core.Models.ReviewResult;

namespace Core.Application.Services.FileReviewer
{
    public interface IFileReviewer
    {
        CsReview Review(string path);
    }
}
