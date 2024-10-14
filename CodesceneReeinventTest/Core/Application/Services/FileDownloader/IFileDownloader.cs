using System.Threading.Tasks;

namespace Core.Application.Services.FileDownloader
{
    public interface IFileDownloader
    {
        Task HandleAsync();
    }
}
