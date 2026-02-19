// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces
{
    /// <summary>
    /// Codescene Logger to send logs to relevant sources.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an error message to file and output window.
        /// </summary>
        void Error(string message, Exception ex);

        /// <summary>
        /// Logs a warning message to file, output and optionally to the status bar.
        /// </summary>
        void Warn(string message, bool statusBar = false);

        /// <summary>
        /// Logs an info message to file, output and optionally to the status bar.
        /// </summary>
        void Info(string message, bool statusBar = false);

        /// <summary>
        /// Logs a debug message to file and output window.
        /// </summary>
        void Debug(string message);
    }
}
