// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Auth
{
    public interface IAuthHost
    {
        Task OpenExternalAsync(string url);

        Task OpenServerSettingAsync();

        Task RevealMonitorAsync();

        Task ShowMessageAsync(string message);
    }
}
