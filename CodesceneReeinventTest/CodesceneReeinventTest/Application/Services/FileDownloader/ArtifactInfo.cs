using System.IO;

namespace CodesceneReeinventTest.Application.Services.FileDownloader
{
    public class ArtifactInfo
    {
        public readonly string ExtensionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        private const string RequiredDevToolsVersion = "7788d46b2a8c65062d79f790444e52b0dd12cfb8";
        private const string Platform = "win32";
        private const string Arch = "x64";

        private string BinaryName => $"cs-{Platform}-{Arch}.exe";
        public string ArtifactName => $"codescene-cli-ide-windows-amd64-{RequiredDevToolsVersion}.zip";

        public string AbsoluteDownloadPath
        {
            get
            {
                return Path.Combine(ExtensionPath, ArtifactName);
            }
        }
        public string AbsoluteBinaryPath
        {
            get
            {
                return Path.Combine(ExtensionPath, BinaryName);
            }
        }
        public string ExecFromZipPath
        {
            get
            {
                return Path.Combine(ExtensionPath, "cs.exe");
            }
        }
    }
}
