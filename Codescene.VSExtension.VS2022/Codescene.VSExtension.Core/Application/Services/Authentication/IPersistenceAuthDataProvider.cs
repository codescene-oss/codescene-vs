using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Services.Authentication
{
    public interface IPersistenceAuthDataProvider
    {
        LoginResponse GetData();
        void Store(LoginResponse data);
        void Clear();
    }
}
