// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class WebComponentFileDataBase
    {
        public string FileName { get; set; }

        public WebComponentFileDataBaseFn Fn { get; set; }
    }
}
