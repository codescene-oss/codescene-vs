// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces;

public interface IProviderKeyStore
{
    IDictionary<string, string> GetAll();
}
