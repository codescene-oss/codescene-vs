using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodesceneReeinventTest;

public class StatusWindow : BaseToolWindow<StatusWindow>
{
    public override string GetTitle(int toolWindowId) => "CodeScene: Status";
    public override Type PaneType => typeof(Pane);
    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken) => Task.FromResult<FrameworkElement>(new StatusWindowControl());

    [Guid("6d1318d4-a780-4942-b4c7-dc785bc1662d")]
    internal class Pane : ToolkitToolWindowPane
    {
        public Pane()
        {
            BitmapImageMoniker = KnownMonikers.ToolWindow;
        }
    }
}
