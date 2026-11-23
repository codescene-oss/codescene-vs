// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces;

public interface IProviderKeyBackingStore
{
    string Get(string name);

    void Set(string name, string value);

    void Delete(string name);
}
