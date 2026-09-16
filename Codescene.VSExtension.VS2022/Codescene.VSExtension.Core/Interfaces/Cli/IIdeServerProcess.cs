// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerProcess : IDisposable
    {
        event EventHandler Exited;

        event EventHandler<IdeServerErrorDataEventArgs> ErrorDataReceived;

        Stream StandardInput { get; }

        Stream StandardOutput { get; }

        Stream StandardError { get; }

        int Id { get; }

        bool HasExited { get; }

        void BeginErrorReadLine();

        void Kill();
    }
}
