// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Controls
{
    public partial class ProviderApiKeyDialog : DialogWindow
    {
        private readonly IProviderKeyStore _store;
        private readonly List<ProviderItemViewModel> _items;

        public ProviderApiKeyDialog(IProviderKeyStore store)
        {
            InitializeComponent();

            _store = store;
            _items = ProviderApiKeyActions.Items(store)
                .Select(item => new ProviderItemViewModel(item))
                .ToList();

            ProviderListBox.ItemsSource = _items;

            if (_items.Count > 0)
            {
                ProviderListBox.SelectedIndex = 0;
            }
        }

        private void ProviderListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = ProviderListBox.SelectedItem != null;
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ProviderListBox.SelectedItem as ProviderItemViewModel;
            if (selectedItem == null)
            {
                return;
            }

            var apiKey = ApiKeyPasswordBox.Password;
            var message = ProviderApiKeyActions.Apply(_store, selectedItem.Provider.Name, apiKey);

            DialogResult = true;
            Close();

            if (!string.IsNullOrEmpty(message))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await VS.StatusBar.ShowMessageAsync(message);
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public class ProviderItemViewModel
        {
            public ProviderItemViewModel(ProviderApiKeyActions.ProviderItem item)
            {
                Provider = item.Provider;
                KeyStored = item.KeyStored;
            }

            public Core.Application.ProviderKey Provider { get; }

            public bool KeyStored { get; }

            public Visibility KeyStoredVisibility => KeyStored ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
