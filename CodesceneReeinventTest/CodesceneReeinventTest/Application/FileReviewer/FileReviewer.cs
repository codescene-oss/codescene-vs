using Core.Application.Services.FileReviewer;
using Core.Application.Services.Mapper;
using Core.Models;
using Core.Models.ReviewResultModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace CodesceneReeinventTest.Application.FileReviewer;


[Export(typeof(IFileReviewer))] // MEF export attribute
[PartCreationPolicy(CreationPolicy.Shared)] // Ensures a single instance
public class FileReviewer : IFileReviewer
{
    const string EXECUTABLE_FILE = "cs-win32-x64.exe";
    [Import(typeof(IModelMapper))]
    private readonly IModelMapper _mapper;

    private static readonly Dictionary<string, ReviewMapModel> ActiveReviewList = [];

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
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found!\n{path}");
        }

        var executionPath = "C:\\"; //Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var exePath = $"{executionPath}\\{EXECUTABLE_FILE}";
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executable file {EXECUTABLE_FILE} can not be found on the location:\n{executionPath}!");
        }
        string arguments = $"review {path} --ide-api";

        var processInfo = new ProcessStartInfo()
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return _mapper.Map(JsonConvert.DeserializeObject<ReviewResultModel>(result));
        }
    }
    public ReviewMapModel Review(string fileName, string content)
    {
        var executionPath = "C:\\";
        var exePath = $"{executionPath}\\{EXECUTABLE_FILE}";
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executable file {EXECUTABLE_FILE} can not be found on the location:\n{executionPath}!");
        }
        string arguments = $"review --ide-api --file-name {fileName}";

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
            if (process.StandardInput != null)
            {
                process.StandardInput.Write(content);
                process.StandardInput.Close(); // Close input stream to signal end of input
            }
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return _mapper.Map(JsonConvert.DeserializeObject<ReviewResultModel>(result));
        }
    }
}