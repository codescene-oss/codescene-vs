using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
public class CodeHealthToolWindow : BaseToolWindow<CodeHealthToolWindow>
{
    public override Type PaneType => typeof(Pane);

    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var ctrl = new WebComponentUserControl(view: "codehealth");

        ctrl.CloseRequested = async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await HideAsync();
        };

        return Task.FromResult<FrameworkElement>(ctrl);
    }

    public override string GetTitle(int toolWindowId) => "Codescene Code Health Monitoring";

    [Guid("7aae282f-1a25-4030-a1b6-3ea8246193f4")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}