using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.ErrorHandling
{
    public interface ILogger
    {
        void Error(string message, Exception ex);
        void Warn(string message);
        void Info(string message);
        void Debug(string message);
        Task LogAsync(string message, Exception ex);
    }
}
