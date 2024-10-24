using CodesceneReeinventTest.Commands;
using CodesceneReeinventTest.Helpers;
using CodesceneReeinventTest.ToolWindows.UserControlWindow;
using System.Windows.Controls;
using System.Windows.Input;

namespace CodesceneReeinventTest.Controls
{

    public partial class UnderlineTaggerTooltip : UserControl
    {
        public class UnderlineTaggerTooltipModel
        {
            public string Category { get; set; }
            public string Details { get; set; }

            public ICommand YourCommand { get; }
            public int WindowId { get; set; }

            public UnderlineTaggerTooltipModel()
            {
                YourCommand = new RelayCommand(ExecuteYourCommand);
            }

            private async void ExecuteYourCommand(object parameter)
            {
                var category = parameter as string;
                if (ToolWindowRegistry.CategoryToIdMap.TryGetValue(category, out int toolWindowId))
                {
                    await UserControlWindow.HideAllUserControlWindowsAsync();
                    await UserControlWindow.ShowAsync(toolWindowId, true);
                }
            }

        }
        public UnderlineTaggerTooltip(string category, string details)
        {
            InitializeComponent();

            DataContext = new UnderlineTaggerTooltipModel
            {
                Category = category,
                Details = details,
            };
        }
    }
}
