using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface ISavedFilesTracker
    {
        IEnumerable<string> GetSavedFiles();
    }
}
