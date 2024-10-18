using System;
using System.Threading.Tasks;

namespace Core.Application.Services.ErrorHandling
{
    public interface IErrorsHandler
    {
        void Log(string message, Exception ex);
        Task LogAsync(string message, Exception ex);
    }
}
