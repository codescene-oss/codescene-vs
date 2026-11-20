// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Models.WebComponent.Message
{
    public class AuthSelectAccountPayload
    {
        public JToken AccountId { get; set; }
    }
}
