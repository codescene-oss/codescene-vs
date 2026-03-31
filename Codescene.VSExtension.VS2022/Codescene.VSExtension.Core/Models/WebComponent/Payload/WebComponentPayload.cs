// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Consts;

namespace Codescene.VSExtension.Core.Models.WebComponent.Payload
{
    public class WebComponentPayload<T>
    {
        public string IdeType { get; set; }

        public string View { get; set; }

        public T Data { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether determines whether additional features for certain WebViews will be shown.
        /// </summary>
        public bool Pro { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether enables developer mode for the WebView. When set to <c>true</c>, this will:
        /// - Log internal state changes and messages to the browser console.
        /// - Show a developer tools icon at the top of each view.
        /// - Allow inspection of the input data passed to the WebView.
        /// Intended for debugging purposes. Should remain <c>false</c> in production.
        /// </summary>
        public bool Devmode { get; set; } = false;

        public List<string> FeatureFlags { get; set; } = new List<string>() { "jobs", "open-settings", "ace-status-indicator" }; // Include loaders feature

        /// <summary>
        /// Helper for creating a Payload for the CWF Web views.
        /// </summary>
        public static WebComponentPayload<T> Create(string view, T data, bool pro = false)
        {
            return new WebComponentPayload<T>
            {
                IdeType = WebComponentConstants.VISUALSTUDIOIDETYPE,
                View = view,
                Pro = pro,
                Data = data,
#if DEBUG
                Devmode = true,
#endif
            };
        }
    }
}
