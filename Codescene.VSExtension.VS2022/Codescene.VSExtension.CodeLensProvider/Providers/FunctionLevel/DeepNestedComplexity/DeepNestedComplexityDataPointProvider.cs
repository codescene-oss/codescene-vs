﻿using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.DeepNestedComplexity
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(DeepNestedComplexityDataPointProvider))]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_TYPESCRIPT)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVASCRIPT)]
    [Priority(1010)]
    public class DeepNestedComplexityDataPointProvider : BaseDataPointProvider<DeepNestedComplexityDataPoint>
    {
        public override string Name => Constants.Titles.DEEP_NESTED_COMPLEXITY;

        [ImportingConstructor]
        public DeepNestedComplexityDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
