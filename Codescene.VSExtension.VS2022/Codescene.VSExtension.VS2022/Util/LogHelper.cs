using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Community.VisualStudio.Toolkit;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util;

public enum LogLevel
{
    Info,
    Warn,
    Error,
    Debug
}

public static class LogHelper
{
    public static async Task LogAsync(string message, LogLevel level, Exception e = null)
    {
        try
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            switch (level)
            {
                case LogLevel.Info: logger.Info(message); break;
                case LogLevel.Warn: logger.Warn(message); break;
                case LogLevel.Error: logger.Error(message, e); break;
                case LogLevel.Debug:
                default: logger.Debug(message); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to log message {message}: {ex.Message}");
        }
    }
}

