namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ISupportedFileChecker
    {
        bool IsSupported(string filePath);
    }
}
