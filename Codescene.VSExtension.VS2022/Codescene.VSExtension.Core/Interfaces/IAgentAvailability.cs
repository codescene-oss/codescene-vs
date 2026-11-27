// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface IAgentAvailability
    {
        event EventHandler Changed;

        bool Enabled { get; }

        bool Authenticated { get; }

        bool Usable { get; }

        AutoRefactorConfig AutoRefactor();

        void NotifySettingsChanged();
    }
}
