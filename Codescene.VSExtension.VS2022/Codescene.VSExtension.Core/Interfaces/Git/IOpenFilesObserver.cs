using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IOpenFilesObserver
    {
        IEnumerable<string> GetAllVisibleFileNames();
    }
}
