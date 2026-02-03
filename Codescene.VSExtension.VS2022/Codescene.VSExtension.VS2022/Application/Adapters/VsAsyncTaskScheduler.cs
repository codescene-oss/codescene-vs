// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IAsyncTaskScheduler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsAsyncTaskScheduler : IAsyncTaskScheduler
    {
        public void Schedule(Func<Task> asyncWork)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await asyncWork();
                }).FileAndForget("VsAsyncTaskScheduler/ScheduledWork");
            }
            catch
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await asyncWork();
                    }
                    catch (Exception)
                    {
                    }
                });
            }
        }
    }
}
