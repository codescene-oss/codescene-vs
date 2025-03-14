namespace Codescene.VSExtension.VS2022.Application.MDFileHandler;

public interface IMDFileHandler
{
    string GetContent(string path, string subPath);
    void SetFileName(string fileName);
}
