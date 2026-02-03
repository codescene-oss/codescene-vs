// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.VS2022.Controls;

namespace Codescene.VSExtension.VS2022.CodeLens
{
    public static class ToolWindowRegistry
    {
        public static readonly Dictionary<int, ToolWindowInfo> ToolWindowCreators = new Dictionary<int, ToolWindowInfo>
        {
            { 1, new ToolWindowInfo(() => new BrainClass(), Constants.Titles.BRAINCLASS) },
            { 2, new ToolWindowInfo(() => new BrainMethod(), Constants.Titles.BRAINMETHOD) },
            { 3, new ToolWindowInfo(() => new BumpyRoadAhead(), Constants.Titles.BUMPYROADAHEAD) },
            { 4, new ToolWindowInfo(() => new CodeDuplication(), Constants.Titles.CODEDUPLICATION) },
            { 5, new ToolWindowInfo(() => new CodeHealthMonitor(), Constants.Titles.CODEHEALTHMONITOR) },
            { 6, new ToolWindowInfo(() => new ComplexConditional(), Constants.Titles.COMPLEXCONDITIONAL) },
            { 7, new ToolWindowInfo(() => new ComplexMethod(), Constants.Titles.COMPLEXMETHOD) },
            { 8, new ToolWindowInfo(() => new ConstructorOverInjection(), Constants.Titles.CONSTRUCTOROVERINJECTION) },
            { 9, new ToolWindowInfo(() => new DeepGlobalNestedComplexity(), Constants.Titles.DEEPGLOBALNESTEDCOMPLEXITY) },
            { 10, new ToolWindowInfo(() => new DeepNestedComplexity(), Constants.Titles.DEEPNESTEDCOMPLEXITY) },
            { 11, new ToolWindowInfo(() => new DuplicatedAssertionBlocks(), Constants.Titles.DUPLICATEDASSERTIONBLOCKS) },
            { 12, new ToolWindowInfo(() => new DuplicatedFunctionBlocks(), Constants.Titles.DUPLICATEDFUNCTIONBLOCKS) },
            { 13, new ToolWindowInfo(() => new ExcessNumberOfFunctionArguments(), Constants.Titles.EXCESSNUMBEROFFUNCTIONARGUMENTS) },
            { 14, new ToolWindowInfo(() => new FileSizeIssue(), Constants.Titles.FILESIZEISSUE) },
            { 15, new ToolWindowInfo(() => new GeneralCodeHealth(), Constants.Titles.GENERALCODEHEALTH) },
            { 16, new ToolWindowInfo(() => new GlobalConditionals(), Constants.Titles.GLOBAlCONDITIONALS) },
            { 17, new ToolWindowInfo(() => new HighDegreeOfCodeDuplication(), Constants.Titles.HIGHDEGREEOFCODEDUPLICATION) },
            { 18, new ToolWindowInfo(() => new LargeAssertionBlocks(), Constants.Titles.LARGEASSERTIONBLOCKS) },
            { 19, new ToolWindowInfo(() => new LargeEmbeddedCodeBlock(), Constants.Titles.LARGEEMBEDDEDCODEBLOCK) },
            { 20, new ToolWindowInfo(() => new LargeMethod(), Constants.Titles.LARGEMETHOD) },
            { 21, new ToolWindowInfo(() => new LinesOfCodeInASingleFile(), Constants.Titles.LINESOFCODEINASINGLEFILE) },
            { 22, new ToolWindowInfo(() => new LinesOfDeclarationsInASingleFile(), Constants.Titles.LINESOFDECLARATIONSINASINGLEFILE) },
            { 23, new ToolWindowInfo(() => new LowCohesion(), Constants.Titles.LOWCOHESION) },
            { 24, new ToolWindowInfo(() => new MissingArgumentsAbstractions(), Constants.Titles.MISSINGARGUMENTSABSTRACTIONS) },
            { 25, new ToolWindowInfo(() => new ModularityIssue(), Constants.Titles.MODULARITYISSUE) },
            { 26, new ToolWindowInfo(() => new NumberOfFunctionsInASingleModule(), Constants.Titles.NUMBEROFFUNCTIONSINASINGLEMODULE) },
            { 27, new ToolWindowInfo(() => new OverallCodeComplexity(), Constants.Titles.OVERALLCODECOMPLEXITY) },
            { 28, new ToolWindowInfo(() => new PotentiallyLowCohesion(), Constants.Titles.POTENTIALLYLOWCOHESION) },
            { 29, new ToolWindowInfo(() => new PrimitiveObsession(), Constants.Titles.PRIMITIVEOBSESSION) },
            { 30, new ToolWindowInfo(() => new StringHeavyFunctionArguments(), Constants.Titles.STRINGHEAVYFUNCTIONARGUMENTS) },
        };

        public static readonly Dictionary<string, int> CategoryToIdMap = ToolWindowCreators.ToDictionary(kvp => kvp.Value.Category, kvp => kvp.Key);

        public class ToolWindowInfo
        {
            public ToolWindowInfo(Func<object> creator, string category)
            {
                Creator = creator;
                Category = category;
            }

            public Func<object> Creator { get; }

            public string Category { get; }
        }
    }
}
