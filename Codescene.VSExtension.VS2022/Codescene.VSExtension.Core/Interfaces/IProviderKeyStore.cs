// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces;

public interface IProviderKeyStore
{
    bool IsStored(string provider);

    string Get(string provider);

    IDictionary<string, string> GetAll();

    void Store(string provider, string value);
}
