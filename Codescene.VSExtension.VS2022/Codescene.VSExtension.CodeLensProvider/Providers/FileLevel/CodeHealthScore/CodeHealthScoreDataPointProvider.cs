using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FileLevel.CodeHealthScore
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodeHealthScoreDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_JS)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [Priority(1000)]
    internal class CodeHealthScoreDataPointProvider : BaseDataPointProvider<CodeHealthScoreDataPoint>
    {

        [ImportingConstructor]
        public CodeHealthScoreDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }

        public override string Name => throw new NotImplementedException();

        public override Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            //var methodsOnly = descriptor.Kind == CodeElementKinds.Type;
            //var codeSceneLensesEnabled = await IsCodelenseEnabledAsync(token);
            //return methodsOnly && codeSceneLensesEnabled;

            //Since Codehealth is score for a whole file it doesn't make sense to show it for a Class and we will skip it for now
            return Task.FromResult(false);
        }
    }
}
