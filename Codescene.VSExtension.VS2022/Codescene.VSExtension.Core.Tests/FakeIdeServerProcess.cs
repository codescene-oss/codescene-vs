// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    internal sealed class FakeIdeServerProcess : IIdeServerProcess
    {
        public FakeIdeServerProcess(Stream input, Stream output)
        {
            StandardInput = input;
            StandardOutput = output;
            StandardError = Stream.Null;
        }

        public event EventHandler Exited;

        public event EventHandler<IdeServerErrorDataEventArgs> ErrorDataReceived;

        public Stream StandardInput { get; }

        public Stream StandardOutput { get; }

        public Stream StandardError { get; }

        public int Id => 1;

        public bool HasExited { get; private set; }

        public void BeginErrorReadLine()
        {
        }

        public void RaiseError(string data)
        {
            ErrorDataReceived?.Invoke(this, new IdeServerErrorDataEventArgs(data));
        }

        public void Kill()
        {
            HasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Kill();
        }
    }
}
