using CodesceneReeinventTest.Core.Models;
using Core.Application.Services.FileReviewer;
using Core.Application.Services.Mapper;
using Core.Models.ReviewResultModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
namespace CodesceneReeinventTest.Application.FileReviewer;


[Export(typeof(IFileReviewer))] // MEF export attribute
[PartCreationPolicy(CreationPolicy.Shared)] // Ensures a single instance
public class FileReviewer : IFileReviewer
{
    const string EXECUTABLE_FILE = "cs-win32-x64.exe";
    [Import(typeof(IModelMapper))]
    private readonly IModelMapper _mapper;

    private static readonly Dictionary<string, ReviewResultModel> ActiveReviewList = [];
    public void AddToActiveReviewList(string documentPath)
    {
        var review = Review(documentPath);
        ActiveReviewList.Add(documentPath, review);
    }
    public void RemoveFromActiveReviewList(string documentPath)
    {
        ActiveReviewList.Remove(documentPath);
    }
    public ReviewResultModel GetReviewObject(string filePath)
    {
        ActiveReviewList.TryGetValue(filePath, out var review);

        //for already opened files on IDE load
        if (review == null)
        {
            AddToActiveReviewList(filePath);
            ActiveReviewList.TryGetValue(filePath, out review);
        }
        var obj = _mapper.Map(review);
        return review;
    }
    public List<TaggerItemModel> GetTaggerItems(string filePath)
    {
        var review = GetReviewObject(filePath);
        var tags = new List<TaggerItemModel>();
        if (review?.FunctionLevelCodeSmells != null)
        {
            foreach (var issues in review.FunctionLevelCodeSmells)
            {
                if (issues?.CodeSmells == null) continue;

                foreach (var function in issues.CodeSmells)
                {
                    var tag = new TaggerItemModel
                    {
                        TooltipText = $"{function.Category} ({function.Details})",
                        StartLine = function.Range.Startline - 1,
                        StartColumn = function.Range.StartColumn - 1,
                        EndLine = function.Range.EndLine - 1,
                        EndColumn = function.Range.EndColumn - 1
                    };
                    tags.Add(tag);
                }
            }
        }
        if (review?.ExpressionLevelCodeSmells != null)
        {
            foreach (var item in review.ExpressionLevelCodeSmells)
            {
                var tag = new TaggerItemModel
                {
                    TooltipText = $"{item.Category} ({item.Details})",
                    StartLine = item.Range.Startline - 1,
                    StartColumn = item.Range.StartColumn - 1,
                    EndLine = item.Range.EndLine - 1,
                    EndColumn = item.Range.EndColumn - 1
                };
                tags.Add(tag);
            }
        }
        return tags;
    }
    public ReviewResultModel Review(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found!\n{path}");
        }

        var executionPath = "C:\\"; //Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var exePath = $"{executionPath}\\{EXECUTABLE_FILE}";
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executable file {EXECUTABLE_FILE} can not be found on the location{executionPath}!");
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
            return JsonConvert.DeserializeObject<ReviewResultModel>(result);
        }
    }
}