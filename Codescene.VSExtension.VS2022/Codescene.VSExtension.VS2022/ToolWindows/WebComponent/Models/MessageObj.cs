// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

internal class MessageObj<T>
{
    public string MessageType { get; set; }

    public T Payload { get; set; }
}
