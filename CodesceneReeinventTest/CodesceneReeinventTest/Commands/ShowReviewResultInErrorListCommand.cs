using CodesceneReeinventTest.Application;
using CodesceneReeinventTest.Application.Services.FileReviewer;
using CodesceneReeinventTest.Commands;

namespace CodesceneReeinventTest;

internal sealed class ShowReviewResultInErrorListCommand(IFileReviewer fileReviewer, IIssuesHandler issuesHandler) : VsCommandBase
{
    internal const int Id = PackageIds.ShowReviewResultInErrorListCommand;

    protected override async void InvokeInternal()
    {
        DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
        if (docView?.TextView == null)
        {
            await VS.MessageBox.ShowAsync("There is no active opened window");
            return;
        }

        var filePath = docView.FilePath;
        var fileName = System.IO.Path.GetFileName(filePath);

        try
        {
            var review = fileReviewer.Review(filePath);
            issuesHandler.Handle(filePath, review);
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowAsync($"File:{fileName}\nPath:{filePath}\nError:{ex.Message}");
        }
    }
}
