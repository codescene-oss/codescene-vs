using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.VS2022.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.VS2022.CodeLens
{
    public static class ToolWindowRegistry
    {
        public class ToolWindowInfo
        {
            public Func<object> Creator { get; }
            public string Category { get; }

            public ToolWindowInfo(Func<object> creator, string category)
            {
                Creator = creator;
                Category = category;
            }
        }

        public static readonly Dictionary<int, ToolWindowInfo> ToolWindowCreators = new Dictionary<int, ToolWindowInfo>
        {
            { 1, new ToolWindowInfo(() => new BrainClass(), Constants.Titles.BRAIN_CLASS) },
            { 2, new ToolWindowInfo(() => new BrainMethod(), Constants.Titles.BRAIN_METHOD) },
            { 3, new ToolWindowInfo(() => new BumpyRoadAhead(), Constants.Titles.BUMPY_ROAD_AHEAD) },
            { 4, new ToolWindowInfo(() => new CodeDuplication(), Constants.Titles.CODE_DUPLICATION) },
            { 5, new ToolWindowInfo(() => new CodeHealthMonitor(), Constants.Titles.CODE_HEALTH_MONITOR) },
            { 6, new ToolWindowInfo(() => new ComplexConditional(), Constants.Titles.COMPLEX_CONDITIONAL) },
            { 7, new ToolWindowInfo(() => new ComplexMethod(), Constants.Titles.COMPLEX_METHOD) },
            { 8, new ToolWindowInfo(() => new ConstructorOverInjection(), Constants.Titles.CONSTRUCTOR_OVER_INJECTION) },
            { 9, new ToolWindowInfo(() => new DeepGlobalNestedComplexity(), Constants.Titles.DEEP_GLOBAL_NESTED_COMPLEXITY) },
            { 10, new ToolWindowInfo(() => new DeepNestedComplexity(), Constants.Titles.DEEP_NESTED_COMPLEXITY) },
            { 11, new ToolWindowInfo(() => new DuplicatedAssertionBlocks(), Constants.Titles.DUPLICATED_ASSERTION_BLOCKS) },
            { 12, new ToolWindowInfo(() => new DuplicatedFunctionBlocks(), Constants.Titles.DUPLICATED_FUNCTION_BLOCKS) },
            { 13, new ToolWindowInfo(() => new ExcessNumberOfFunctionArguments(), Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS) },
            { 14, new ToolWindowInfo(() => new FileSizeIssue(), Constants.Titles.FILE_SIZE_ISSUE) },
            { 15, new ToolWindowInfo(() => new GeneralCodeHealth(), Constants.Titles.GENERAL_CODE_HEALTH) },
            { 16, new ToolWindowInfo(() => new GlobalConditionals(), Constants.Titles.GLOBAl_CONDITIONALS) },
            { 17, new ToolWindowInfo(() => new HighDegreeOfCodeDuplication(), Constants.Titles.HIGH_DEGREE_OF_CODE_DUPLICATION) },
            { 18, new ToolWindowInfo(() => new LargeAssertionBlocks(), Constants.Titles.LARGE_ASSERTION_BLOCKS) },
            { 19, new ToolWindowInfo(() => new LargeEmbeddedCodeBlock(), Constants.Titles.LARGE_EMBEDDED_CODE_BLOCK) },
            { 20, new ToolWindowInfo(() => new LargeMethod(), Constants.Titles.LARGE_METHOD) },
            { 21, new ToolWindowInfo(() => new LinesOfCodeInASingleFile(), Constants.Titles.LINES_OF_CODE_IN_A_SINGLE_FILE) },
            { 22, new ToolWindowInfo(() => new LinesOfDeclarationsInASingleFile(), Constants.Titles.LINES_OF_DECLARATIONS_IN_A_SINGLE_FILE) },
            { 23, new ToolWindowInfo(() => new LowCohesion(), Constants.Titles.LOW_COHESION) },
            { 24, new ToolWindowInfo(() => new MissingArgumentsAbstractions(), Constants.Titles.MISSING_ARGUMENTS_ABSTRACTIONS) },
            { 25, new ToolWindowInfo(() => new ModularityIssue(), Constants.Titles.MODULARITY_ISSUE) },
            { 26, new ToolWindowInfo(() => new NumberOfFunctionsInASingleModule(), Constants.Titles.NUMBER_OF_FUNCTIONS_IN_A_SINGLE_MODULE) },
            { 27, new ToolWindowInfo(() => new OverallCodeComplexity(), Constants.Titles.OVERALL_CODE_COMPLEXITY) },
            { 28, new ToolWindowInfo(() => new PotentiallyLowCohesion(), Constants.Titles.POTENTIALLY_LOW_COHESION) },
            { 29, new ToolWindowInfo(() => new PrimitiveObsession(), Constants.Titles.PRIMITIVE_OBSESSION) },
            { 30, new ToolWindowInfo(() => new StringHeavyFunctionArguments(), Constants.Titles.STRING_HEAVY_FUNCTION_ARGUMENTS) },
        };
        public static readonly Dictionary<string, int> CategoryToIdMap = ToolWindowCreators.ToDictionary(kvp => kvp.Value.Category, kvp => kvp.Key);
    }
}
