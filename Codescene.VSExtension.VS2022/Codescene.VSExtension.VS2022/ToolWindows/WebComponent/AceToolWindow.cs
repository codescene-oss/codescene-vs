using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
public class AceToolWindow : BaseToolWindow<AceToolWindow>
{

    public override Type PaneType => typeof(Pane);

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        string localFolder = Path.Combine(exeFolder, "ToolWindows\\WebComponent");

        var reviewer = await VS.GetMefServiceAsync<ICodeReviewer>();
        var refactoredCode = reviewer.GetCachedRefactoredCode();

        var payload = new WebComponentPayload
        {
            IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
            View = WebComponentConstants.VievTypes.ACE,
            Data = new WebComponentData
            {
                Loading = false,
                FileData = new WebComponentFileData
                {
                    Filename = "CustomLegends.ts",
                    FunctionName = "extract_identifiers",
                    LineNumber = 11,
                    Action = new WebComponentAction
                    {
                        GoToFunctionLocationPayload = "path/to/CustomLegends.ts:extract_identifiers"
                    }
                },
                AceResultData = refactoredCode
            }
        };

        var ctrl = new WebComponentUserControl(payload)
        {
            CloseRequested = async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await HideAsync();
            }
        };

        return ctrl;
    }

    public override string GetTitle(int toolWindowId) => "Refactoring suggestion";

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}