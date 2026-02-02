using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface IAsyncTaskScheduler
    {
        void Schedule(Func<Task> asyncWork);
    }
}
