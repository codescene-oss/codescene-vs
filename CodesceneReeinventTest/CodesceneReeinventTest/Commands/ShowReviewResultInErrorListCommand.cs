
using CodesceneReeinventTest.Commands;
using Core.Application.Services.ErrorHandling;
using Core.Application.Services.FileReviewer;
using Core.Application.Services.IssueHandler;

namespace CodesceneReeinventTest;

internal sealed class ShowReviewResultInErrorListCommand(IFileReviewer fileReviewer, IIssuesHandler issuesHandler, IErrorsHandler errorsHandler) : VsCommandBase
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
            var message = $"File:{fileName}\nPath:{filePath}\nError:{ex.Message}";
            await errorsHandler.LogAsync(message, ex);
            await VS.MessageBox.ShowAsync(message);
        }
    }
}
