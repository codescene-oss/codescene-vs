using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(ILogger))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class Logger : ILogger
{
    public void Error(string message, Exception ex)
    {
        ex.Log();
    }

    public void Info(string message)
    {
        Console.WriteLine(message);
    }

    public async Task LogAsync(string message, Exception ex)
    {
        await ex.LogAsync();
    }
}
