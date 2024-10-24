using CodesceneReeinventTest.Controls;
using CodesceneReeinventTest.Helpers;
using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace CodesceneReeinventTest.ToolWindows.UserControlWindow
{
    public class UserControlWindow : BaseToolWindow<UserControlWindow>
    {
        public override string GetTitle(int toolWindowId)
        {
            if (ToolWindowRegistry.ToolWindowCreators.TryGetValue(toolWindowId, out var creator))
            {
                return "CodeScene - " + creator.Category;
            }
            return "CodeScene - Issue";
        }
        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            if (ToolWindowRegistry.ToolWindowCreators.TryGetValue(toolWindowId, out var creator))
            {
                return (FrameworkElement)creator.Creator();
            }
            return new GeneralCodeHealth();
        }
        // Give this a new unique guid
        [Guid("d3b3ebd9-87d1-41cd-bf84-268d88953417")]
        internal class Pane : ToolWindowPane
        {
            public Pane()
            {
                // Set an image icon for the tool window
                BitmapImageMoniker = KnownMonikers.StatusInformation;

            }
        }
    }
}
