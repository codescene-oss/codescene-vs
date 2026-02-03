// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Authentication;
using Codescene.VSExtension.Core.Models;
using Meziantou.Framework.Win32;

namespace Codescene.VSExtension.CredentialManagerPersistenceAuthProvider
{
    [Export(typeof(IPersistenceAuthDataProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CredentialManagerProvider : IPersistenceAuthDataProvider
    {
        public const string APPLICATIONNAME = "vs-codescene-extension";

        public void Clear()
        {
            var storedData = CredentialManager.ReadCredential(APPLICATIONNAME);
            if (storedData != null)
            {
                CredentialManager.DeleteCredential(APPLICATIONNAME);
            }
        }

        public LoginResponse GetData()
        {
            var storedData = CredentialManager.ReadCredential(APPLICATIONNAME);
            if (storedData == null)
            {
                return null;
            }

            return new LoginResponse
            {
                Name = storedData.UserName,
                Token = storedData.Password,
                UserId = storedData.Comment,
            };
        }

        public void Store(LoginResponse data)
        {
            CredentialManager.WriteCredential(
                applicationName: APPLICATIONNAME,
                userName: data.Name,
                secret: data.Token,
                comment: data.UserId,
                persistence: CredentialPersistence.LocalMachine);
        }
    }
}
