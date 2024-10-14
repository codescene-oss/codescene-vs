using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Core.Application.Services.FileReviewer;

namespace CodesceneReeinventTest;

[Command(PackageIds.ReviewActiveFileCommand)]
internal sealed class ReviewActiveFileCommand(DIToolkitPackage package, IFileReviewer fileReviewer) : BaseDICommand(package)
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
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
            await VS.MessageBox.ShowAsync($"File:{fileName}\nPath:{filePath}\nError:{ex.Message}");
        }
    }
}
