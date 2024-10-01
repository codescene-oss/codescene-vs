

namespace CodesceneReeinventTest.Application.Handlers;

public interface IMDFileHandler
{
    string GetContent(string path, string subPath);
    void SetFileName(string fileName);
}
