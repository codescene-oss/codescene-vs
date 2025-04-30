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

    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        string localFolder = Path.Combine(exeFolder, "ToolWindows\\WebComponent");
        string data = File.ReadAllText(Path.Combine(localFolder, "test_data.json"));
        //var validator = new JsonSchemaValidator();
        //var result = validator.Validate(data);
        var ctrl = new WebComponentUserControl(view: "ace", data: data);

        ctrl.CloseRequested = async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await HideAsync();
        };

        return Task.FromResult<FrameworkElement>(ctrl);
    }

    public override string GetTitle(int toolWindowId) => "Refactoring suggestion";

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}