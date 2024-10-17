
using CodesceneReeinventTest.Commands;
using Core.Application.Services.ErrorHandling;
using Core.Application.Services.FileReviewer;

namespace CodesceneReeinventTest;

internal sealed class ReviewActiveFileCommand(IFileReviewer fileReviewer, IErrorsHandler errorsHandler) : VsCommandBase
{
    internal const int Id = PackageIds.ReviewActiveFileCommand;

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
            var message = $"{fileName} - score:{review.Score}";
            await VS.MessageBox.ShowAsync(message);
        }
        catch (Exception ex)
        {
            var message = $"File:{fileName}\nPath:{filePath}\nError:{ex.Message}";
            await errorsHandler.LogAsync(message, ex);
            await VS.MessageBox.ShowAsync(message);
        }
    }
}
