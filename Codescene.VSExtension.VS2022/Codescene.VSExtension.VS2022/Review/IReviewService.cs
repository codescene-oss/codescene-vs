using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Review
{
    public interface IReviewService
    {
        Task ReviewContentAsync(string path, ITextBuffer buffer);
    }
}
