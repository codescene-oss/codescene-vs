using CodesceneReeinventTest.Application.Handlers;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodesceneReeinventTest.ToolWindows.Markdown
{
    public class MarkdownWindow : BaseToolWindow<MarkdownWindow>
    {
        public IMDFileHandler _mdFileHandler;
        public static string FileName { get; set; }
        public override string GetTitle(int toolWindowId) => "CodeScene: Markdown";

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            IToolkitServiceProvider<CodesceneReeinventTestPackage> serviceProvider = await VS.GetServiceAsync<SToolkitServiceProvider<CodesceneReeinventTestPackage>, IToolkitServiceProvider<CodesceneReeinventTestPackage>>();
            _mdFileHandler = serviceProvider.GetService<IMDFileHandler>();
            return (new MarkdownWindowControl(_mdFileHandler, FileName));
        }
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
