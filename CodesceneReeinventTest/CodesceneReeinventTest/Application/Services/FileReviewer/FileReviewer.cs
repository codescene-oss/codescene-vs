namespace CodesceneReeinventTest.Application.Services.FileReviewer;

using CodeLensShared;
using Newtonsoft.Json;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;

[Export(typeof(IFileReviewer))] // MEF export attribute
[PartCreationPolicy(CreationPolicy.Shared)] // Ensures a single instance
internal class FileReviewer : IFileReviewer
{
    const string EXECUTABLE_FILE = "cs-win32-x64.exe";
    public CsReview Review(string path)
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

        return JsonConvert.DeserializeObject<CsReview>(result);
    }
}
