using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Interfaces.Authentication
{
    public interface IPersistenceAuthDataProvider
    {
        LoginResponse GetData();

        void Store(LoginResponse data);

        void Clear();
    }
}
