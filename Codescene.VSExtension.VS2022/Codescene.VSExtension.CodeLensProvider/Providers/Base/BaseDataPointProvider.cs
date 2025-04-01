using Codescene.VSExtension.Core.Application.Services.Codelens;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.Base
{
    public abstract class BaseDataPointProvider<T> : IAsyncCodeLensDataPointProvider where T : class, IBaseDataPoint
    {
        public abstract string Name { get; }
        protected readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public BaseDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) => _callbackService = callbackService;

        private Task<TResult> InvokeMethodAsync<TResult>(string name, CancellationToken token, IReadOnlyList<object> parameters = null) => _callbackService.Value.InvokeAsync<TResult>(this, name, parameters, token);

        protected Task<bool> IsCodelenseEnabledAsync(CancellationToken token) => InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.IsCodeSceneLensesEnabled), token);

        public virtual async Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            // Add codelense only for methods
            if (descriptor.Kind != CodeElementKinds.Method)
            {
                return false;
            }

            // Check if codelense is enabled in settings
            var codeSceneLensesEnabled = await IsCodelenseEnabledAsync(token);
            if (!codeSceneLensesEnabled)
            {
                return false;
            }

            descriptorContext.Properties.TryGetValue("StartLine", out dynamic zeroBasedLineNumber);

            var parameters = new object[] { Name, descriptor.FilePath,
                       (int)zeroBasedLineNumber + 1, //Since it's 0-based it should be increment for 1
                       descriptorContext.Properties };

            return await InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.ShowCodeLensForLine), token, parameters);
        }

        public virtual async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var vsPid = await InvokeMethodAsync<int>(nameof(ICodesceneCodelensCallbackService.GetVisualStudioPid), token);

            var dataPoint = (T)Activator.CreateInstance(typeof(T), descriptor, _callbackService.Value);

            _ =  _callbackService
                .Value.InvokeAsync(
                    this,
                    nameof(ICodesceneCodelensCallbackService.InitializeRpcAsync),
                    new[] { dataPoint.DataPointId },
                    token
                )
                .ConfigureAwait(false);

            var connection = new VisualStudioConnection(dataPoint, vsPid);
            await connection.ConnectAsync(token);
            dataPoint.VsConnection = connection;

            return dataPoint;
        }
    }
}
