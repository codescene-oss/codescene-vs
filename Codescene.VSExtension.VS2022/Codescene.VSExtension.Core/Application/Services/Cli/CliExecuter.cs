using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecuter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecuter : ICliExecuter
    {
        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;

        private static readonly Dictionary<string, ReviewMapModel> ActiveReviewList = new Dictionary<string, ReviewMapModel>();

        public void AddToActiveReviewList(string documentPath)
        {
            var review = Review(documentPath);
            ActiveReviewList.Add(documentPath, review);
        }
        public void AddToActiveReviewList(string documentPath, string content)
        {
            var review = Review(documentPath, content);
            ActiveReviewList[documentPath] = review;
        }
        public void RemoveFromActiveReviewList(string documentPath)
        {
            ActiveReviewList.Remove(documentPath);
        }
        public ReviewMapModel GetReviewObject(string filePath)
        {
            ActiveReviewList.TryGetValue(filePath, out var review);

            //for already opened files on IDE load
            if (review == null)
            {
                AddToActiveReviewList(filePath);
                ActiveReviewList.TryGetValue(filePath, out review);
            }
            return review;
        }
        public List<ReviewModel> GetTaggerItems(string filePath)
        {
            var review = GetReviewObject(filePath);
            return review.ExpressionLevel.Concat(review.FunctionLevel).ToList();
        }

        public ReviewMapModel Review(string path)
        {
            string arguments = _cliCommandProvider.GetReviewPathCommand(path);
            var result = ExecuteCommand(arguments);
            return _mapper.Map(JsonConvert.DeserializeObject<ReviewResultModel>(result));
        }

        public ReviewMapModel Review(string path, string content)
        {
            string arguments = _cliCommandProvider.GetReviewFileContentCommand(path);
            var result = ExecuteCommand(arguments, content: content);
            return _mapper.Map(JsonConvert.DeserializeObject<ReviewResultModel>(result));
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