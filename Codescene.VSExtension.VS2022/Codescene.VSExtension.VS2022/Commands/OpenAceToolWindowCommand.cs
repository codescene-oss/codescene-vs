using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.OpenAceToolWindow)]
internal sealed class OpenAceToolWindowCommand : BaseCommand<OpenAceToolWindowCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e) => await AceToolWindow.ShowAsync();
}