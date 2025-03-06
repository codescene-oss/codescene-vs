using CodesceneReeinventTest.ToolWindows.Spike;
using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodesceneReeinventTest.ToolWindows.Status;

public class SpikeWindow : BaseToolWindow<SpikeWindow>
{
    public override string GetTitle(int toolWindowId) => "Spike title";
    public override Type PaneType => typeof(Pane);
    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        => Task.FromResult<FrameworkElement>(new SpikeWIndowControl());

    [Guid("d64879a0-6207-47a1-be87-70e6edff7754")]
    internal class Pane : ToolkitToolWindowPane
    {
        public Pane()
        {
            BitmapImageMoniker = KnownMonikers.ToolWindow;
        }
    }
}
