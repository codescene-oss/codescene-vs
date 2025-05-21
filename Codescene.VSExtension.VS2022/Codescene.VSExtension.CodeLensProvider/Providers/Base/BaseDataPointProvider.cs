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


        private bool IsAllowedKind(CodeElementKinds kind) => kind == CodeElementKinds.Method || kind == CodeElementKinds.Function;
        private bool IsNotAllowedKind(CodeElementKinds kind) => !IsAllowedKind(kind);

        protected async Task<TResult> SafeInvokeMethodAsync<TResult>(string methodName, CancellationToken token, IReadOnlyList<object> parameters = null)
        {
            try
            {
                return await _callbackService.Value.InvokeAsync<TResult>(
                    this,
                    methodName,
                    parameters ?? Array.Empty<object>(),
                    token
                );
            }
            catch (ObjectDisposedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeInvokeMethodAsync ObjectDisposed: {ex.Message}");
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeInvokeMethodAsync Exception: {ex}");

                try
                {
                    var safeMessage = $"Method: SafeInvokeMethodAsync, MethodName: {methodName}, Error: {ex.Message}";
                    await _callbackService.Value.InvokeAsync<bool>(this, nameof(ICodesceneCodelensCallbackService.SendError), new object[] { safeMessage }, token);
                }
                catch (Exception nestedEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error while reporting exception: {nestedEx.Message}");
                }

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
                    return false;

                if (!TryGetLineNumber(descriptorContext, out var lineNumber))
                    return false;

                var parameters = new object[] { Name, descriptor.FilePath, lineNumber };

                var show = await SafeInvokeMethodAsync<bool>(
                    nameof(ICodesceneCodelensCallbackService.ShowCodeLensForFunction),
                    token,
                    parameters
                );

                return show;
            }
            catch (Exception ex)
            {
                await SafeInvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.SendError), token, new object[] { ex.ToString() });
                return false;
            }
        }

        private bool TryGetLineNumber(CodeLensDescriptorContext context, out int lineNumber)
        {
            lineNumber = default;

            if (!context.Properties.TryGetValue("StartLine", out var value))
                return false;

            switch (value)
            {
                case int i:
                    lineNumber = i + 1;
                    return true;
                case long l:
                    lineNumber = (int)l + 1;
                    return true;
                default:
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

            _ = SafeInvokeMethodAsync<bool>(nameof(ICodesceneCodelensCallbackService.InitializeRpcAsync), token, parameters: new[] { dataPoint.DataPointId });

            var connection = new VisualStudioConnection(dataPoint, vsPid);
            await connection.ConnectAsync(token);
            dataPoint.VsConnection = connection;

            return dataPoint;
        }
    }
}
