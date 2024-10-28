using Core.Models;

namespace Core.Application.Services.Authentication
{
    public class FakeAuthDataProvider : IPersistenceAuthDataProvider
    {
        public void Clear()
        {
            return;
        }

        public LoginResponse GetData()
        {
            return new LoginResponse
            {
                Name = "amina@reeinvent.com",
                Token = "token",
                UserId = "1234"
            };
        }

        public void Store(LoginResponse data)
        {
            return;
        }
    }
}
