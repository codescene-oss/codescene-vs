using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.MethodIssue
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name("MethodIssueDataPointProvider")]
    [ContentType("csharp")]
    [Priority(100)]
    internal class MethodIssueDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        private readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public MethodIssueDataPointProvider(Lazy<ICodeLensCallbackService> callbackService)
        {
            _callbackService = callbackService;
        }
        public async Task<bool> CanCreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var methodsOnly = descriptor.Kind == CodeElementKinds.Method;
            if (!methodsOnly) return false;

            return true;
        }
        public async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var dataPoint = new MethodIssueDataPoint(descriptor, _callbackService.Value);
            return dataPoint;
        }
    }
}
