// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.VS2022.Options;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands
{
    [Command(PackageGuids.CodeSceneCmdSetString, PackageIds.StopAutomaticAnalysis)]
    internal sealed class StopAutomaticAnalysisCommand : BaseCommand<StopAutomaticAnalysisCommand>
    {
        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            return SetAutomaticAnalysisAsync(false);
        }

        internal static async Task SetAutomaticAnalysisAsync(bool enabled)
        {
            var general = General.Instance;
            general.EnableAutomaticAnalysis = enabled;
            await general.SaveAsync();
        }
    }

    [Command(PackageGuids.CodeSceneCmdSetString, PackageIds.StartAutomaticAnalysis)]
    internal sealed class StartAutomaticAnalysisCommand : BaseCommand<StartAutomaticAnalysisCommand>
    {
        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            return StopAutomaticAnalysisCommand.SetAutomaticAnalysisAsync(true);
        }
    }
}
