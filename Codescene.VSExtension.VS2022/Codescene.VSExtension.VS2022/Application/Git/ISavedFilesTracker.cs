using System.Collections.Generic;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    public interface ISavedFilesTracker
    {
        IEnumerable<string> GetSavedFiles();
    }
}
