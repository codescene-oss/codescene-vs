using System.IO;

namespace CodesceneReeinventTest.Application.Services.FileDownloader
{
    public class ArtifactInfo
    {
        private readonly string _extensionPath;
        const string REQUIRED_DEVTOOLS_VERSION = "7788d46b2a8c65062d79f790444e52b0dd12cfb8";
        const string Platform = "win32";
        const string Arch = "x64";
        string ArtifactName = "codescene-cli-ide-windows-amd64-" + REQUIRED_DEVTOOLS_VERSION + ".zip";
        public ArtifactInfo(string extensionPath)
        {
            _extensionPath = extensionPath;
        }
        public string getAbsoluteDownloadPath()
        {
            return Path.Combine(_extensionPath, ArtifactName);
        }
        public string getAbsoluteBinaryPath()
        {
            return Path.Combine(_extensionPath);
        }
    }
}
