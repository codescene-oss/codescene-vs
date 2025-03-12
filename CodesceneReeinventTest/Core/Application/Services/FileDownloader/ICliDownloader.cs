using System.Threading.Tasks;

namespace Core.Application.Services.FileDownloader
{
    public interface ICliDownloader
    {
        Task DownloadOrUpgradeAsync();
    }
}
