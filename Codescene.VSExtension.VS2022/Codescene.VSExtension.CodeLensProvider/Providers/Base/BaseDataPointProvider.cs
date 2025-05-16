using Codescene.VSExtension.CodeLensProvider.Abstraction;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Newtonsoft.Json;
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

        private bool IsAllowedKind(CodeElementKinds kind) => kind == CodeElementKinds.Method || kind == CodeElementKinds.Function;
        private bool IsNotAllowedKind(CodeElementKinds kind) => !IsAllowedKind(kind);

        protected async Task<TResult> SafeInvokeMethodAsync<TResult>(string methodName, CancellationToken token, IReadOnlyList<object> parameters = null)
        {
            try
            {
                return await _callbackService.Value.InvokeAsync<TResult>(this, methodName, parameters, token);
            }
            catch (ObjectDisposedException ex)
            {
                // Optional: log or trace for diagnostics
                System.Diagnostics.Debug.WriteLine($"SafeInvokeMethodAsync:{JsonConvert.SerializeObject(ex)}");
                return default;
            }
            catch (Exception ex)
            {
                // Silently ignore or log if needed
                System.Diagnostics.Debug.WriteLine($"SafeInvokeMethodAsync:{JsonConvert.SerializeObject(ex)}");
                _ = _callbackService.Value.InvokeAsync<bool>(this, nameof(ICodesceneCodelensCallbackService.ThrowException), new object[] { ex }, token);
                return default;
            }
        }

        protected async Task<bool> IsCodelenseEnabledAsync(CodeLensDescriptor descriptor, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return false;

            // Add codelense only for methods and functions
            if (IsNotAllowedKind(descriptor.Kind))
            {
                return false;
            }

            var isEnabledInSettings = await SafeInvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.IsCodeSceneLensesEnabled), token);

            return isEnabledInSettings;
        }

        public virtual async Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return false;

            try
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
            catch (Exception ex)
            {
                await InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.ThrowException), token, new object[] { ex });
                return false;
            }
        }

        public virtual async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            var vsPid = await SafeInvokeMethodAsync<int>(nameof(ICodesceneCodelensCallbackService.GetVisualStudioPid), token);

            if (vsPid == 0)
                return null;

            var dataPoint = (T)Activator.CreateInstance(typeof(T), descriptor, _callbackService.Value);

            _ = InvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.InitializeRpcAsync), token, parameters: new[] { dataPoint.DataPointId });

            var connection = new VisualStudioConnection(dataPoint, vsPid);
            await connection.ConnectAsync(token);
            dataPoint.VsConnection = connection;

            return dataPoint;
        }
    }
}
