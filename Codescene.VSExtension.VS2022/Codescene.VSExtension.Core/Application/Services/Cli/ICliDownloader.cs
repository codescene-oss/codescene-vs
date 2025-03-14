using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliDownloader
    {
        Task DownloadAsync();
    }
}
