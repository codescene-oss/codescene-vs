using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Commands
{
    [Command(PackageGuids.CodeSceneCmdSetString, PackageIds.OpenCodeSceneToolWindow)]
    internal sealed class OpenCodeSceneToolWindowCommand : BaseCommand<OpenCodeSceneToolWindowCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();
            logger?.Info("Opening Code Health Monitor tool window...");

            try
            {
                await CodeSceneToolWindow.ShowAsync();
            }
            catch (Exception ex)
            {
                logger?.Error("Could not open CodeScene tool window.", ex);
            }
        }
    }
}
