using System.IO;

namespace CodesceneReeinventTest.Application.Services.FileDownloader
{
    public class ArtifactInfo
    {
        public readonly string ExtensionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        private const string REQUIRED_DEV_TOOLS_VERSION = "7788d46b2a8c65062d79f790444e52b0dd12cfb8";
        private const string PLATFORM = "win32";
        private const string ARCH = "x64";

        private string BinaryName => $"cs-{PLATFORM}-{ARCH}.exe";
        public string ArtifactName => $"codescene-cli-ide-windows-amd64-{REQUIRED_DEV_TOOLS_VERSION}.zip";
        public string AbsoluteDownloadPath => Path.Combine(ExtensionPath, ArtifactName);
        public string AbsoluteBinaryPath => Path.Combine(ExtensionPath, BinaryName);
        public string ExecFromZipPath => Path.Combine(ExtensionPath, "cs.exe");
    }
}
