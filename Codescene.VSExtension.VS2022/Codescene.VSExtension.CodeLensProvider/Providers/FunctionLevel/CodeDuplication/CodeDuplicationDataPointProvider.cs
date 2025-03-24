using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.CodeDuplication
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodeDuplicationDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(1030)]
    public class CodeDuplicationDataPointProvider : BaseDataPointProvider<CodeDuplicationDataPoint>
    {
        public override string Name => Constants.Titles.CODE_DUPLICATION;

        [ImportingConstructor]
        public CodeDuplicationDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
