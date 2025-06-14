﻿using Codescene.VSExtension.VS2022.Commands;

namespace Codescene.VSExtension.VS2022;

internal class OpenStatusWindowCommand : VsCommandBase
{
    internal const int Id = PackageIds.OpenStatusWindowCommand;
    protected override async void InvokeInternal()
    {
        await StatusWindow.ShowAsync();
    }
}
