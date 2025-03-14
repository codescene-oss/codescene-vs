using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.ErrorHandling
{
    public interface ILogger
    {
        void Error(string message, Exception ex);
        void Info(string message);
        Task LogAsync(string message, Exception ex);
    }
}
