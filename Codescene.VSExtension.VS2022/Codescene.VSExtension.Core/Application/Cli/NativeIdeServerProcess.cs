// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal sealed class NativeIdeServerProcess : IIdeServerProcess
    {
        private readonly Process _process;
        private readonly StringBuilder _stderr = new StringBuilder();

        public NativeIdeServerProcess(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _process.EnableRaisingEvents = true;
            _process.Exited += (_, __) => Exited?.Invoke(this, EventArgs.Empty);
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (_stderr.Length < 4000)
                {
                    var remaining = 4000 - _stderr.Length;
                    _stderr.Append(e.Data.Length <= remaining ? e.Data : e.Data.Substring(0, remaining));
                }

                ErrorDataReceived?.Invoke(this, new IdeServerErrorDataEventArgs(e.Data));
            };
        }

        public event EventHandler Exited;

        public event EventHandler<IdeServerErrorDataEventArgs> ErrorDataReceived;

        public Stream StandardInput => _process.StandardInput.BaseStream;

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

        public Stream StandardError => _process.StandardError.BaseStream;

        public int Id => _process.Id;

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch
                {
                    return true;
                }
            }
        }

        public string StderrSnapshot => _stderr.ToString();

        public void BeginErrorReadLine()
        {
            _process.BeginErrorReadLine();
        }

        public void Kill()
        {
            if (HasExited)
            {
                return;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT && _process.Id > 0)
            {
                try
                {
                    var killer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/pid " + _process.Id + " /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });
                    killer?.WaitForExit(5000);
                    return;
                }
                catch
                {
                }
            }

            try
            {
                _process.Kill();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Kill();
            _process.Dispose();
        }
    }
}
