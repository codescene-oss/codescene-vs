using CodesceneReeinventTest.Controls;
using CodesceneReeinventTest.Helpers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
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
        public static async Task HideAllUserControlWindowsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null) return;

            // Enumerate through all tool windows
            IEnumWindowFrames windowFramesEnum;
            uiShell.GetToolWindowEnum(out windowFramesEnum);

            IVsWindowFrame[] frames = new IVsWindowFrame[1];
            while (windowFramesEnum.Next(1, frames, out uint fetched) == VSConstants.S_OK && fetched == 1)
            {
                // Check if the tool window is of type UserControlWindow
                var frame = frames[0];
                frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView);

                if (docView is UserControlWindow.Pane)
                {
                    frame.Hide();
                }
            }
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
