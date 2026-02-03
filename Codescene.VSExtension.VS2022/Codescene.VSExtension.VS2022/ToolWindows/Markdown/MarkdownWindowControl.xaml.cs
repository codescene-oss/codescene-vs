// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Controls;
using Codescene.VSExtension.Core.Interfaces.Util;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.ToolWindows.Markdown
{
    public partial class MarkdownWindowControl : UserControl
    {
        [Import]
        private IMDFileHandler _mdFileHandler;

        private string _fileName;

        public MarkdownWindowControl(string fileName)
        {
            InitializeComponent();
            _fileName = fileName;
            InitializeFileHandler();
        }

        public void InitializeFileHandler()
        {
            if (_fileName != string.Empty)
            {
                _mdFileHandler.SetFileName(_fileName);
            }

            string htmlContent = _mdFileHandler.GetContent("Resources", null);

            SetWebBrowserContent(htmlContent);
        }

        private void SetWebBrowserContent(string htmlContent)
        {
            // Get Visual Studio theme colors
            var backgroundBrush = (System.Windows.Media.SolidColorBrush)FindResource(VsBrushes.WindowKey);
            var foregroundBrush = (System.Windows.Media.SolidColorBrush)FindResource(VsBrushes.WindowTextKey);

            var backgroundColor = ColorTranslator.ToHtml(Color.FromArgb(backgroundBrush.Color.A, backgroundBrush.Color.R, backgroundBrush.Color.G, backgroundBrush.Color.B));
            var textColor = ColorTranslator.ToHtml(Color.FromArgb(foregroundBrush.Color.A, foregroundBrush.Color.R, foregroundBrush.Color.G, foregroundBrush.Color.B));

            // needed for '
            string meta = "<meta charset=\"UTF-8\">";

            // Inject CSS to match the VS theme
            string css = $@"
            <style>
                body {{
                    background-color: {backgroundColor};
                    color: {textColor};
                    font-family: 'Segoe UI'
                }}
            </style>";

            // Combine CSS with HTML content
            string htmlWithStyles = css + meta + htmlContent;

            // Display content in the WebBrowser
            WebBrowser.NavigateToString(htmlWithStyles);
        }
    }
}
