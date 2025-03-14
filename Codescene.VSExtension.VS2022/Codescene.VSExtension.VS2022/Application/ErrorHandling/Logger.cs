using Codescene.VSExtension.Core.Application.Services.ErrorHandling;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;
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
