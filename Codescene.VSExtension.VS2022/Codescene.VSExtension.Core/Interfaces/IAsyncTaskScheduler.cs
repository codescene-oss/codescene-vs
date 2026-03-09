// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface IAsyncTaskScheduler
    {
        void Schedule(Func<Task> asyncWork);

        void Schedule(Func<CancellationToken, Task> asyncWork);

        void Schedule(Func<CancellationToken, Task> asyncWork, CancellationToken cancellationToken);
    }
}
