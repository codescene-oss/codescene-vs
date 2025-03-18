namespace Codescene.VSExtension.Core.Application.Services.MDFileHandler
{
    public interface IMDFileHandler
    {
        string GetContent(string path, string subPath);
        void SetFileName(string fileName);
    }
}