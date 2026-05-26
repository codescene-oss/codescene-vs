// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;

namespace Codescene.VSExtension.Core.Util;

public static class ExternalNavigationAllowlist
{
    private static readonly string[] AllowedHosts =
    {
        "refactoring.com",
        "en.wikipedia.org",
        "codescene.io",
        "codescene.com",
        "blog.ploeh.dk",
        "supporthub.codescene.com",
        "forms.clickup.com",
        "helpcenter.codescene.com",
    };

    public static bool IsAllowedExternalUri(string uriString)
    {
        if (string.IsNullOrWhiteSpace(uriString))
        {
            return false;
        }

        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            return false;
        }

        var host = uri.IdnHost;
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        return AllowedHosts.Any(allowed => IsHostMatch(host, allowed));
    }

    private static bool IsHostMatch(string host, string allowedHost)
    {
        return string.Equals(host, allowedHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + allowedHost, StringComparison.OrdinalIgnoreCase);
    }
}
