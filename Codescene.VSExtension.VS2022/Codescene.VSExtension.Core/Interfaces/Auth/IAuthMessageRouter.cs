// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Interfaces.Auth
{
    public interface IAuthMessageRouter
    {
        Task<bool> TryRouteAsync(string messageType, JToken payload);
    }
}
