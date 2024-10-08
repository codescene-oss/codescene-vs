using CodeLensShared;

namespace CodesceneReeinventTest.Application.Services.FileReviewer;


internal interface IFileReviewer
{
    CsReview Review(string path);
}
