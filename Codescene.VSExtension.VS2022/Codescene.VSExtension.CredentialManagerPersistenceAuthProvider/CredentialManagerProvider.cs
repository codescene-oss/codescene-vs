﻿using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Models;
using Meziantou.Framework.Win32;

namespace Codescene.VSExtension.CredentialManagerPersistenceAuthProvider
{
    public class CredentialManagerProvider : IPersistenceAuthDataProvider
    {
        public const string APPLICATION_NAME = "vs-codescene-extension";
        public void Clear()
        {
            var storedData = CredentialManager.ReadCredential(APPLICATION_NAME);
            if (storedData != null)
            {
                CredentialManager.DeleteCredential(APPLICATION_NAME);
            }
        }

        public LoginResponse GetData()
        {
            var storedData = CredentialManager.ReadCredential(APPLICATION_NAME);
            if (storedData == null)
            {
                return null;
            }

            return new LoginResponse
            {
                Name = storedData.UserName,
                Token = storedData.Password,
                UserId = storedData.Comment
            };
        }

        public void Store(LoginResponse data)
        {
            CredentialManager.WriteCredential(
                applicationName: APPLICATION_NAME,
                userName: data.Name,
                secret: data.Token,
                comment: data.UserId,
                persistence: CredentialPersistence.LocalMachine);
        }
    }
}
