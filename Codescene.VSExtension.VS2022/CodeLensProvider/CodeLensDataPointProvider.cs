using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(Id)]
    [ContentType("CSharp")]
    [LocalizedName(typeof(Resource), Id)]
    [Priority(210)]
    public class CodeLensDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        internal const string Id = "CustomCodeLensProvider";

        public Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var methodsOnly = descriptor.Kind == CodeElementKinds.Method;
            return Task.FromResult(methodsOnly);
        }

        public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult<IAsyncCodeLensDataPoint>(new CodeLensDataPoint(descriptor));
        }
    }

}
