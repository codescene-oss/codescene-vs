using Core.Models;

namespace Core.Application.Services.Authentication
{
    public interface IAuthenticationService
    {
        bool IsLoggedIn();
        bool Login(string serverUrl);
        LoginResponse GetData();
        void SignOut();
    }
}
