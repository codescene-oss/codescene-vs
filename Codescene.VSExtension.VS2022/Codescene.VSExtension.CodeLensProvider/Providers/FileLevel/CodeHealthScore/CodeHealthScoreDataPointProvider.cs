using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FileLevel.CodeHealthScore
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodeHealthScoreDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(1000)]
    internal class CodeHealthScoreDataPointProvider : BaseDataPointProvider<CodeHealthScoreDataPoint>
    {

        [ImportingConstructor]
        public CodeHealthScoreDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }

        public override string Name => throw new NotImplementedException();

        public override async Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var methodsOnly = descriptor.Kind == CodeElementKinds.Type;
            var codeSceneLensesEnabled = await IsCodelenseEnabledAsync();
            return methodsOnly && codeSceneLensesEnabled;
        }
    }
}
