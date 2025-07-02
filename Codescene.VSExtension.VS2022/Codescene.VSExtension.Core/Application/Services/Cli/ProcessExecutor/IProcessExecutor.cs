using System;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface IProcessExecutor
    {
        string Execute(string arguments, string content = null, TimeSpan? timeout = null);
    }
}