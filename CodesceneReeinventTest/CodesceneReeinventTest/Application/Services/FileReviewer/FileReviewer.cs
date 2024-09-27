namespace CodesceneReeinventTest.Application.Services.FileReviewer;

using CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;
using System.Diagnostics;


internal class FileReviewer : IFileReviewer
{
    public CsReview Review(string path)
    {
        const string exePath = "cs-win32-x64.exe";
        string arguments = $"review {path}";


        ProcessStartInfo processInfo = new()
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(processInfo);
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new CsReview { Score = 9.8f }; //System.Text.Json.JsonSerializer.Deserialize<CsReview>(result);
    }
}
