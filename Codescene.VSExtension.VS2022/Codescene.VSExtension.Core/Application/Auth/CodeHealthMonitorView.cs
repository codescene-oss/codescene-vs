// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class CodeHealthMonitorView
    {
        public static string For(AuthSnapshot snapshot)
        {
            if (AuthWebviewFields.ToAuthPayload(snapshot) != null && snapshot.Surface == AuthConstants.Surfaces.AUTH)
            {
                return WebComponentConstants.ViewTypes.AUTH;
            }

            return WebComponentConstants.ViewTypes.HOME;
        }

        public static WebComponentPayload<CodeHealthMonitorComponentData> Payload(string view, CodeHealthMonitorComponentData data)
        {
            return WebComponentPayload<CodeHealthMonitorComponentData>.Create(
                view,
                view == WebComponentConstants.ViewTypes.AUTH ? null : data,
                true);
        }
    }
}
