using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Core.Application.Services.FileReviewer;
using Core.Application.Services.IssueHandler;

namespace CodesceneReeinventTest;

[Command(PackageIds.ShowReviewResultInErrorListCommand)]
internal sealed class ShowReviewResultInErrorListCommand(DIToolkitPackage package, IFileReviewer fileReviewer, IIssuesHandler issuesHandler) : BaseDICommand(package)
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
            issuesHandler.Handle(filePath, review);
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowAsync($"File:{fileName}\nPath:{filePath}\nError:{ex.Message}");
        }
    }
}
