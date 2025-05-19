using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FileLevel.CodeHealthScore
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodeHealthScoreDataPointProvider))]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_TYPESCRIPT)]
    [ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVASCRIPT)]
    [Priority(1000)]
    internal class CodeHealthScoreDataPointProvider : BaseDataPointProvider<CodeHealthScoreDataPoint>
    {

        [ImportingConstructor]
        public CodeHealthScoreDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }

        public override string Name => throw new NotImplementedException();
    }
}
