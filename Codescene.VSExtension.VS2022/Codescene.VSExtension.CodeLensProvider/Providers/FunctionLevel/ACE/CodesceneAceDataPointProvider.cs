using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.ACE
{

    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodesceneAceDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(980)]
    public class CodesceneAceDataPointProvider : BaseDataPointProvider<CodesceneAceDataPoint>
    {
        public override string Name => Constants.Titles.CODESCENE_ACE;

        [ImportingConstructor]
        public CodesceneAceDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }

        public override async Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var enabled = await IsCodelenseEnabledAsync(descriptor, token);
            if (!enabled)
            {
                return false;
            }

            //return descriptorContext.Properties.ContainsKey(CODESCENE_LENS_PRESENT);
            return true;
        }
    }
}
