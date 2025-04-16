using Codescene.VSExtension.CodeLensProvider.Abstraction;
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
        protected const string CODESCENE_LENS_PRESENT = "CodeScene:LensPresent";
        public abstract string Name { get; }

        protected readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public BaseDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) => _callbackService = callbackService;

        private Task<TResult> InvokeMethodAsync<TResult>(string name, CancellationToken token, IReadOnlyList<object> parameters = null) => _callbackService.Value.InvokeAsync<TResult>(this, name, parameters, token);

        protected async Task<bool> IsCodelenseEnabledAsync(CodeLensDescriptor descriptor, CancellationToken token)
        {
            // Add codelense only for methods
            if (descriptor.Kind != CodeElementKinds.Method)
            {
                return false;
            }

            var isEnabledInSettings = await InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.IsCodeSceneLensesEnabled), token);

            return isEnabledInSettings;
        }

        public virtual async Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var enabled = await IsCodelenseEnabledAsync(descriptor, token);
            if (!enabled)
            {
                return false;
            }

            descriptorContext.Properties.TryGetValue("StartLine", out dynamic zeroBasedLineNumber);

            var lineNumber = (int)zeroBasedLineNumber + 1;

            var parameters = new object[] { Name, descriptor.FilePath, lineNumber };

            var show = await InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.ShowCodeLensForFunction), token, parameters);

            return show;
        }

        public virtual async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var vsPid = await InvokeMethodAsync<int>(nameof(ICodesceneCodelensCallbackService.GetVisualStudioPid), token);

            var dataPoint = (T)Activator.CreateInstance(typeof(T), descriptor, _callbackService.Value);

            _ = InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.InitializeRpcAsync), token, parameters: new[] { dataPoint.DataPointId });

            var connection = new VisualStudioConnection(dataPoint, vsPid);
            await connection.ConnectAsync(token);
            dataPoint.VsConnection = connection;

            return dataPoint;
        }
    }
}
