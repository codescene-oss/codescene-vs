// Copyright (c) CodeScene. All rights reserved.

using System;
using Microsoft.VisualStudio.Shell.TableManager;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;

internal class CustomErrorListEntry : ITableEntry
{
    private object errorCode = null;

    public object Identity => throw new NotImplementedException();

    public bool CanSetValue(string keyName)
    {
        return keyName switch
        {
            StandardTableKeyNames.ErrorCode => true,
            _ => false,
        };
    }

    public bool TryGetValue(string keyName, out object content)
    {
        switch (keyName)
        {
            case StandardTableKeyNames.ErrorCode:
                content = errorCode;
                return true;
            default:
                content = string.Empty;
                return false;
        }
    }

    public bool TrySetValue(string keyName, object content)
    {
        throw new NotImplementedException();
    }
}
