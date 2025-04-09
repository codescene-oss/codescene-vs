using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;
namespace Codescene.VSExtension.VS2022.Commands;

[Command(PackageGuids.CodeSceneCmdSetString, PackageIds.OpenSettings)]
internal sealed class OpenSettingsCommand : BaseCommand<OpenSettingsCommand>
{
    //protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    //{
    //    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    //    VS2022Package.Instance.ShowOptionPage(typeof(OptionsProvider.GeneralOptions));
    //}
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
    }

}