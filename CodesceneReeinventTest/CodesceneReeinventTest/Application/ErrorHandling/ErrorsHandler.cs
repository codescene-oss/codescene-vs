using Core.Application.Services.ErrorHandling;

namespace CodesceneReeinventTest.Application.ErrorHandling;
internal class ErrorsHandler : IErrorsHandler
{
    public void Log(string message, Exception ex)
    {
        ex.Log();
    }

    public async Task LogAsync(string message, Exception ex)
    {
        await ex.LogAsync();
    }
}
