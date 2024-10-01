using CodesceneReeinventTest.ToolWindows.Markdown;
using Community.VisualStudio.Toolkit.DependencyInjection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;


namespace CodesceneReeinventTest
{
    [Command(PackageIds.OpenMarkdownWindowCommand)]
    internal sealed class OpenMarkdownWindowCommand : BaseDICommand
    {
        private readonly string _fileName;
        public OpenMarkdownWindowCommand(DIToolkitPackage package, string fileName) : base(package)
        {
             _fileName = fileName;
        }
        public async Task OpenAsync(OleMenuCmdEventArgs e)
        {
            MarkdownWindow.FileName = _fileName;
            await MarkdownWindow.ShowAsync();
        }
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MarkdownWindow.ShowAsync();
        }
    }
}
