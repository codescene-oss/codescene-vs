using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.Imaging;
using CodesceneReeinventTest.Application.Services.Authentication;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CodesceneReeinventTest
{
    public class StatusWindow : BaseToolWindow<StatusWindow>
    {
        public IAuthenticationService _authenticationService;
        public override string GetTitle(int toolWindowId) => "CodeScene: Status";

        public override Type PaneType => typeof(Pane);
        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            //One solution
            IToolkitServiceProvider<CodesceneReeinventTestPackage> serviceProvider = await VS.GetServiceAsync<SToolkitServiceProvider<CodesceneReeinventTestPackage>, IToolkitServiceProvider<CodesceneReeinventTestPackage>>();
            _authenticationService = serviceProvider.GetService<IAuthenticationService>();
            return (new StatusWindowControl(_authenticationService));
            //return Task.FromResult<FrameworkElement>(new StatusWindowControl());
        }
        [Guid("6d1318d4-a780-4942-b4c7-dc785bc1662d")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.ToolWindow;
            }
        }
    }
}
