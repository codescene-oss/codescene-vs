using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Newtonsoft.Json;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]

namespace Codescene.VSExtension.Core.Application.Cli
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
            var cliFilePath = _cliSettingsProvider.CliFileFullPath;

            if (!File.Exists(cliFilePath))
            {
                throw new FileNotFoundException(
                    $"CodeScene CLI executable not found at {cliFilePath}. " +
                    "The CLI should be bundled with the extension. " +
                    "Please reinstall the extension or contact support if this issue persists.",
                    cliFilePath);
            }

            var actualTimeout = timeout ?? Constants.Timeout.DEFAULTCLITIMEOUT;

            var processInfo = new ProcessStartInfo
            {
                FileName = cliFilePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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

                var timeoutArgs = new WaitForProcessOrTimeoutArgs()
                {
                    Process = process,
                    OutputTcs = outputTcs,
                    ErrorTcs = errorTcs,
                    Timeout = actualTimeout,
                    Command = arguments,
                };
                WaitForProcessOrTimeout(timeoutArgs);

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

        private void WaitForProcessOrTimeout(WaitForProcessOrTimeoutArgs arguments)
        {
            var waitTask = Task.Run(() =>
            {
                arguments.Process.WaitForExit();
                arguments.OutputTcs.Task.Wait();
                arguments.ErrorTcs.Task.Wait();
            });

            if (!waitTask.Wait(arguments.Timeout))
            {
                try
                {
                    arguments.Process.Kill();
                }
                catch
                {
                    return;
                }
                // Ignore telemetry timeouts. Also prevent potential infinite loop since we send timeouts to Amplitude.
                if (arguments.Command.Contains("telemetry")) return;

                throw new TimeoutException($"Process execution exceeded the timeout of {arguments.Timeout.TotalMilliseconds}ms.");
            }
        }

        private string HandleResult(Process process, StringBuilder output, StringBuilder error)
        {
            var code = process.ExitCode;
            if (code != 0)
            {
                if (code == 10)
                {
                    throw JsonConvert.DeserializeObject<DevtoolsException>(output.ToString());
                }

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

    public class WaitForProcessOrTimeoutArgs
    {
        public Process Process { get; set; }
        public TaskCompletionSource<bool> OutputTcs { get; set; }
        public TaskCompletionSource<bool> ErrorTcs { get; set; }
        public TimeSpan Timeout { get; set; }
        public string Command { get; set; }

        public WaitForProcessOrTimeoutArgs() { }
    }
}
