using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecuter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecuter : ICliExecuter
    {
        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;


        public CliReviewModel Review(string path)
        {
            string arguments = _cliCommandProvider.GetReviewPathCommand(path);
            var result = ExecuteCommand(arguments);
            return JsonConvert.DeserializeObject<CliReviewModel>(result);
        }

        public CliReviewModel ReviewContent(string filename, string content)
        {
            string arguments = _cliCommandProvider.GetReviewFileContentCommand(filename);
            var result = ExecuteCommand(arguments, content: content);
            return JsonConvert.DeserializeObject<CliReviewModel>(result);
        }

        private string ExecuteCommand(string arguments, string content = null)
        {
            var exePath = _cliSettingsProvider.CliFileFullPath;
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable file {exePath} can not be found on the location!");
            }

            var processInfo = new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process.StandardInput != null && content != null)
                {
                    process.StandardInput.Write(content);
                    process.StandardInput.Close(); // Close input stream to signal end of input
                }
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }
        }

        public string GetFileVersion()
        {
            string arguments = _cliCommandProvider.VersionCommand;
            var result = ExecuteCommand(arguments);
            return result.TrimEnd('\r', '\n');
        }
    }
}