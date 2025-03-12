using System.Threading.Tasks;

namespace Core.Application.Services.FileDownloader
{
    public interface ICliExecutableHandler
    {
        Task DownloadAsync();
        Task UpgradeFileVersionIfNecessaryAsync();
    }
}
