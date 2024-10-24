using CodesceneReeinventTest.Controls;
using Microsoft.VisualStudio.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace CodesceneReeinventTest.ToolWindows.UserControlWindow
{
    public class UserControlWindow : BaseToolWindow<UserControlWindow>
    {
        private static readonly Dictionary<int, Func<object>> ToolWindowCreators = new Dictionary<int, Func<object>>
        {
            { 1, () => new BrainClass() },
            { 2, () => new BrainMethod() },
            { 3, () => new BumpyRoadAhead() },
            { 4, () => new CodeDuplication() },
            { 5, () => new CodeHealthMonitor() },
            { 6, () => new ComplexConditional() },
            { 7, () => new ComplexMethod() },
            { 8, () => new ConstructorOverInjection() },
            { 9, () => new DeepGlobalNestedComplexity() },
            { 10, () => new DeepNestedComplexity() },
            { 11, () => new DuplicatedAssertionBlocks() },
            { 12, () => new DuplicatedFunctionBlocks() },
            { 13, () => new ExcessNumberOfFunctionArguments() },
            { 14, () => new FileSizeIssue() },
            { 15, () => new GeneralCodeHealth() },
            { 16, () => new GlobalConditionals() },
            { 17, () => new HighDegreeOfCodeDuplication() },
            { 18, () => new LargeAssertionBlocks() },
            { 19, () => new LargeEmbeddedCodeBlock() },
            { 20, () => new LargeMethod() },
            { 21, () => new LinesOfCodeInASingleFile() },
            { 22, () => new LinesOfDeclarationsInASingleFile() },
            { 23, () => new LowCohesion() },
            { 24, () => new MissingArgumentsAbstractions() },
            { 25, () => new ModularityIssue() },
            { 26, () => new NumberOfFunctionsInASingleModule() },
            { 27, () => new OverallCodeComplexity() },
            { 28, () => new PotentiallyLowCohesion() },
            { 29, () => new PrimitiveObsession() },
            { 30, () => new StringHeavyFunctionArguments() },
        };

        public override string GetTitle(int toolWindowId) => $"Issue (CodeScene)";
        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            if (ToolWindowCreators.TryGetValue(toolWindowId, out var creator))
            {
                return (FrameworkElement)creator();
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
