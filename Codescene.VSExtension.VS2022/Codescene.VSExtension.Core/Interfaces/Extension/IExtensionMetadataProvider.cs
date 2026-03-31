// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Extension
{
    public interface IExtensionMetadataProvider
    {
        string GetVersion();

        string GetDisplayName();

        string GetDescription();

        string GetPublisher();
    }
}
