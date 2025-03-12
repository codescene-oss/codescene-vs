using System.IO;

namespace Core.Application.Services.FileDownloader
{
    public class ArtifactInfo
    {
        public static readonly string ExtensionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public const string REQUIRED_DEV_TOOLS_VERSION = "3b28b97d2f4a17d596c6f2ec5cf2e86363c08d21";
        private const string PLATFORM = "win32";
        private const string ARCH = "x64";
        private static string BinaryName => $"cs-{PLATFORM}-{ARCH}.exe";
        public static string ArtifactName => $"cs-ide-windows-amd64-{REQUIRED_DEV_TOOLS_VERSION}.zip";
        public static string ArtifactURL => $"{ArtifactBaseURL}{ArtifactName}";
        private static string ArtifactBaseURL => "https://downloads.codescene.io/enterprise/cli/";
        public static string AbsoluteDownloadPath => Path.Combine(ExtensionPath, ArtifactName);
        public static string ABSOLUTE_CLI_FILE_PATH => Path.Combine(ExtensionPath, BinaryName);
        public static string ExecFromZipPath => Path.Combine(ExtensionPath, "cs-ide.exe");
    }
}