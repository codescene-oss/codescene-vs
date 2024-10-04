using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodesceneReeinventTest.ToolWindows.Markdown
{
    public class MarkdownWindow : BaseToolWindow<MarkdownWindow>
    {
        public static string FileName { get; set; }
        public override string GetTitle(int toolWindowId) => "CodeScene: Markdown";
        public override Type PaneType => typeof(Pane);
        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken) => Task.FromResult<FrameworkElement>(new MarkdownWindowControl());

        [Guid("282d9eff-5009-4652-aacc-a86e89b9cf2f")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.ToolWindow;
            }
        }
    }
}
