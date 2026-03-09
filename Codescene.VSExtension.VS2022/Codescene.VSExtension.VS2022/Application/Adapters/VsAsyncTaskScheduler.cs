// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IAsyncTaskScheduler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsAsyncTaskScheduler : IAsyncTaskScheduler, IDisposable
    {
        private readonly JoinableTaskCollection _collection;
        private readonly JoinableTaskFactory _factory;

        public VsAsyncTaskScheduler()
        {
            var context = ThreadHelper.JoinableTaskContext;
            _collection = context.CreateCollection();
            _factory = context.CreateFactory(_collection);
        }

        public void Schedule(Func<Task> asyncWork)
        {
            _factory.RunAsync(async () =>
            {
                await asyncWork();
            }).FileAndForget("VsAsyncTaskScheduler/ScheduledWork");
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork)
        {
            var token = VS2022Package.Instance?.PackageDisposalToken ?? CancellationToken.None;
            Schedule(asyncWork, token);
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork, CancellationToken cancellationToken)
        {
            _factory.RunAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await asyncWork(cancellationToken);
            }).FileAndForget("VsAsyncTaskScheduler/ScheduledWork");
        }

        public async Task DrainAsync()
        {
            await _collection.JoinTillEmptyAsync();
        }

        public void Dispose()
        {
            (_collection as IDisposable)?.Dispose();
        }
    }
}
