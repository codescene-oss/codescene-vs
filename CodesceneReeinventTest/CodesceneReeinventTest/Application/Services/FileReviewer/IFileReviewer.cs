using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;

namespace CodesceneReeinventTest.Application.Services.FileReviewer;


internal interface IFileReviewer
{
    CsReview Review(string path);
}
