using Core.Models;

namespace Core.Application.Services.Authentication
{
    public interface IPersistenceAuthDataProvider
    {
        LoginResponse GetData();
        void Store(LoginResponse data);
        void Clear();
    }
}
