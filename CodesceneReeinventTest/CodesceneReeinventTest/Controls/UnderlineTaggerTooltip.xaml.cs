using CodeLensProvider.Providers.Base;
using CodesceneReeinventTest.Application.MDFileHandler;
using CodesceneReeinventTest.Commands;
using CodesceneReeinventTest.ToolWindows.UserControlWindow;
using System.Collections.Generic;
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
            public readonly IMDFileHandler _mDFileHandler;

            public UnderlineTaggerTooltipModel()
            {
                _mDFileHandler = CodesceneReeinventTestPackage.GetService<IMDFileHandler>();
                YourCommand = new RelayCommand(ExecuteYourCommand);
            }
            private static readonly Dictionary<string, int> CategoryToIdMap = new Dictionary<string, int>
            {
                { Constants.Titles.BRAIN_CLASS, 1 },
                { Constants.Titles.BRAIN_METHOD, 2 },
                { Constants.Titles.BUMPY_ROAD_AHEAD, 3 },
                { Constants.Titles.CODE_DUPLICATION, 4 },
                { Constants.Titles.CODE_HEALTH_MONITOR, 5 },
                { Constants.Titles.COMPLEX_CONDITIONAL, 6 },
                { Constants.Titles.COMPLEX_METHOD, 7 },
                { Constants.Titles.CONSTRUCTOR_OVER_INJECTION, 8 },
                { Constants.Titles.DEEP_GLOBAL_NESTED_COMPLEXITY, 9 },
                { Constants.Titles.DEEP_NESTED_COMPLEXITY, 10 },
                { Constants.Titles.DUPLICATED_ASSERTION_BLOCKS, 11 },
                { Constants.Titles.DUPLICATED_FUNCTION_BLOCKS, 12 },
                { Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS, 13 },
                { Constants.Titles.FILE_SIZE_ISSUE, 14 },
                { Constants.Titles.GENERAL_CODE_HEALTH, 15 },
                { Constants.Titles.GLOBAl_CONDITIONALS, 16 },
                { Constants.Titles.HIGH_DEGREE_OF_CODE_DUPLICATION, 17 },
                { Constants.Titles.LARGE_ASSERTION_BLOCKS, 18 },
                { Constants.Titles.LARGE_EMBEDDED_CODE_BLOCK, 19 },
                { Constants.Titles.LARGE_METHOD, 20 },
                { Constants.Titles.LINES_OF_CODE_IN_A_SINGLE_FILE, 21 },
                { Constants.Titles.LINES_OF_DECLARATIONS_IN_A_SINGLE_FILE, 22 },
                { Constants.Titles.LOW_COHESION, 23 },
                { Constants.Titles.MISSING_ARGUMENTS_ABSTRACTIONS, 24 },
                { Constants.Titles.MODULARITY_ISSUE, 25 },
                { Constants.Titles.NUMBER_OF_FUNCTIONS_IN_A_SINGLE_MODULE, 26 },
                { Constants.Titles.OVERALL_CODE_COMPLEXITY, 27 },
                { Constants.Titles.POTENTIALLY_LOW_COHESION, 28 },
                { Constants.Titles.PRIMITIVE_OBSESSION, 29 },
                { Constants.Titles.STRING_HEAVY_FUNCTION_ARGUMENTS, 30 }
            };
            private async void ExecuteYourCommand(object parameter)
            {
                var category = parameter as string;
                if (CategoryToIdMap.TryGetValue(category, out int toolWindowId))
                {
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
