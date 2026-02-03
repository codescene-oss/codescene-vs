// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface ILogger
    {
        void Error(string message, Exception ex);

        void Warn(string message);

        void Info(string message);

        void Debug(string message);
    }
}
