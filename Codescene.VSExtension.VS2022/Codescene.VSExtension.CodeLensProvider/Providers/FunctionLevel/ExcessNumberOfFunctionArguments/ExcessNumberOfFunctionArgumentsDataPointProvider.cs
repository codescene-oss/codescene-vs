using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.ExcessNumberOfFunctionArguments
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(ExcessNumberOfFunctionArgumentsDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [ContentType(Constants.CONTENT_TYPE_JAVASCRIPT)]
    [Priority(1050)]
    public class ExcessNumberOfFunctionArgumentsDataPointProvider : BaseDataPointProvider<ExcessNumberOfFunctionArgumentsDataPoint>
    {
        public override string Name => Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS;

        [ImportingConstructor]
        public ExcessNumberOfFunctionArgumentsDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
