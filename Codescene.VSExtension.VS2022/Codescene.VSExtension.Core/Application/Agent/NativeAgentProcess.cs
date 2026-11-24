// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal sealed class NativeAgentProcess : IAgentProcess
    {
        private const int JobObjectLimitKillOnJobClose = 0x00002000;

        private static readonly TimeSpan OutputDrainGrace = TimeSpan.FromSeconds(10);

        private readonly Process _process;
        private readonly ILogger _logger;
        private readonly TaskCompletionSource<int> _exitTcs = new TaskCompletionSource<int>();
        private readonly TaskCompletionSource<bool> _stdoutClosed = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> _stderrClosed = new TaskCompletionSource<bool>();
        private readonly object _readLock = new object();
        private Task<int> _completion;
        private IntPtr _jobHandle = IntPtr.Zero;

        public NativeAgentProcess(Process process, ILogger logger)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _process.OutputDataReceived += (_, e) => OnLine(e.Data, StdoutLine, _stdoutClosed);
            _process.ErrorDataReceived += (_, e) => OnLine(e.Data, StderrLine, _stderrClosed);

            _process.EnableRaisingEvents = true;
            _process.Exited += OnExited;

            TryAssignToJobObject();

            if (_process.HasExited)
            {
                OnExited(_process, EventArgs.Empty);
            }
        }

        public event EventHandler<string> StdoutLine;

        public event EventHandler<string> StderrLine;

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
        }

        public Task<int> WaitForExitAsync()
        {
            lock (_readLock)
            {
                if (_completion == null)
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _completion = CompleteAfterOutputDrainedAsync();
                }

                return _completion;
            }
        }

        public void KillTree()
        {
            if (_process.HasExited)
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
            KillTree();
            _process.Dispose();

            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(
            IntPtr hJob,
            JobObjectInfoType infoType,
            IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private void OnLine(string line, EventHandler<string> handler, TaskCompletionSource<bool> closed)
        {
            if (line == null)
            {
                closed.TrySetResult(true);
                return;
            }

            handler?.Invoke(this, line);
        }

        private async Task<int> CompleteAfterOutputDrainedAsync()
        {
            var exitCode = await _exitTcs.Task.ConfigureAwait(false);
            var drained = Task.WhenAll(_stdoutClosed.Task, _stderrClosed.Task);
            if (await Task.WhenAny(drained, Task.Delay(OutputDrainGrace)).ConfigureAwait(false) != drained)
            {
                _logger.Debug("cs-agent exited but its output streams stayed open; continuing without the remaining output");
            }

            return exitCode;
        }

        private void OnExited(object sender, EventArgs e)
        {
            try
            {
                _exitTcs.TrySetResult(_process.ExitCode);
            }
            catch (InvalidOperationException ex)
            {
                _exitTcs.TrySetException(ex);
            }
        }

        private void TryAssignToJobObject()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            try
            {
                var jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (jobHandle == IntPtr.Zero)
                {
                    _logger.Debug("Failed to create job object");
                    return;
                }

                var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JobObjectLimitKillOnJobClose,
                };

                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = info,
                };

                var length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                var extendedInfoPtr = Marshal.AllocHGlobal(length);

                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                    if (!SetInformationJobObject(
                        jobHandle,
                        JobObjectInfoType.ExtendedLimitInformation,
                        extendedInfoPtr,
                        (uint)length))
                    {
                        _logger.Debug("Failed to set job object information");
                        CloseHandle(jobHandle);
                        return;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }

                if (!AssignProcessToJobObject(jobHandle, _process.Handle))
                {
                    _logger.Debug("Failed to assign process to job object");
                    CloseHandle(jobHandle);
                    return;
                }

                _jobHandle = jobHandle;
            }
            catch (Exception ex)
            {
                _logger.Debug("Exception while assigning process to job object: " + ex.Message);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public int LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public int ActiveProcessLimit;
            public UIntPtr Affinity;
            public int PriorityClass;
            public int SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
