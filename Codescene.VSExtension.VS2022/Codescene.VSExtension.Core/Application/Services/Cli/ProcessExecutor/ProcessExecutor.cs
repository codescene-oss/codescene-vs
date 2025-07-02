using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(IProcessExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class ProcessExecutor : IProcessExecutor
    {
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [ImportingConstructor]
        public ProcessExecutor(ICliSettingsProvider cliSettingsProvider)
        {
            _cliSettingsProvider = cliSettingsProvider ?? throw new ArgumentNullException(nameof(cliSettingsProvider));
        }

        public string Execute(string arguments, string content = null, TimeSpan? timeout = null)
        {
            var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);

            var processInfo = new ProcessStartInfo
            {
                FileName = _cliSettingsProvider.CliFileFullPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                var outputTcs = new TaskCompletionSource<bool>();
                var errorTcs = new TaskCompletionSource<bool>();

                AttachOutputHandlers(new AttachOutputHandlersArgs(process, outputBuilder, errorBuilder, outputTcs, errorTcs));

                process.Start();

                WriteInput(process, content);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                WaitForProcessOrTimeout(process, outputTcs, errorTcs, actualTimeout);

                return HandleResult(process, outputBuilder, errorBuilder);
            }
        }

        private void AttachOutputHandlers(AttachOutputHandlersArgs handlerArguments)
        {
            handlerArguments.Process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) handlerArguments.OutputTcs.TrySetResult(true);
                else handlerArguments.Output.AppendLine(e.Data);
            };

            handlerArguments.Process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) handlerArguments.ErrorTcs.TrySetResult(true);
                else handlerArguments.Error.AppendLine(e.Data);
            };
        }

        private void WriteInput(Process process, string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                process.StandardInput.Write(content);
                process.StandardInput.Close();
            }
        }

        private void WaitForProcessOrTimeout(Process process,
            TaskCompletionSource<bool> outputTcs,
            TaskCompletionSource<bool> errorTcs,
            TimeSpan timeout)
        {
            var waitTask = Task.Run(() =>
            {
                process.WaitForExit();
                outputTcs.Task.Wait();
                errorTcs.Task.Wait();
            });

            if (!waitTask.Wait(timeout))
            {
                try { process.Kill(); }
                catch (Exception ex)
                {
                    throw new TimeoutException("Process timed out and could not be killed.", ex);
                }

                throw new TimeoutException($"Process execution exceeded the timeout of {timeout.TotalMilliseconds}ms.");
            }
        }

        private string HandleResult(Process process, StringBuilder output, StringBuilder error)
        {
            if (error.Length > 0)
            {
                throw new InvalidOperationException($"Process error output: {error}");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"Process exited with code {process.ExitCode}. Error: {error}");
            }

            return output.ToString();
        }
    }

    internal class AttachOutputHandlersArgs
    {
        public Process Process { get; set; }
        public StringBuilder Output { get; set; }
        public StringBuilder Error { get; set; }
        public TaskCompletionSource<bool> OutputTcs { get; set; }
        public TaskCompletionSource<bool> ErrorTcs { get; set; }

        public AttachOutputHandlersArgs(
            Process process,
            StringBuilder output,
            StringBuilder error,
            TaskCompletionSource<bool> outputTcs,
            TaskCompletionSource<bool> errorTcs)
        {
            Process = process;
            Output = output;
            Error = error;
            OutputTcs = outputTcs;
            ErrorTcs = errorTcs;
        }

    }
}