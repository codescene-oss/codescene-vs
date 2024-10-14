using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodesceneReeinventTest.ToolWindows.Problems;
public class ProblemsWindow : BaseToolWindow<ProblemsWindow>
{
    public override string GetTitle(int toolWindowId) => "Problems";

    public override Type PaneType => typeof(Pane);

    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken) => Task.FromResult<FrameworkElement>(new ProblemsWindowControl());

    [Guid("b529151b-4cd9-402c-afac-ae59112c4e2b")]
    internal class Pane : ToolkitToolWindowPane
    {
        public Pane()
        {
            BitmapImageMoniker = KnownMonikers.ToolWindow;
        }
    }
}
