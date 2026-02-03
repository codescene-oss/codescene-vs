// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces.Authentication;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Authentication
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
                UserId = "1234",
            };
        }

        public void Store(LoginResponse data)
        {
            return;
        }
    }
}
