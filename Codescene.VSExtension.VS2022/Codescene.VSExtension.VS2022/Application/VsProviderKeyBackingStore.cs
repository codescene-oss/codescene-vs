// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using CodeSceneConstants = Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application;

[Export(typeof(IProviderKeyBackingStore))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class VsProviderKeyBackingStore : IProviderKeyBackingStore
{
    private readonly ILogger _logger;

    [ImportingConstructor]
    public VsProviderKeyBackingStore(ILogger logger)
    {
        _logger = logger;
    }

    public string Get(string name)
    {
        try
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var store = GetOrCreateSettingsStore();
                return store.PropertyExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION, name)
                    ? store.GetString(CodeSceneConstants.Titles.SETTINGSCOLLECTION, name)
                    : null;
            });
        }
        catch (Exception e)
        {
            _logger?.Error($"Failed to retrieve provider key from settings store.", e);
            return null;
        }
    }

    public void Set(string name, string value)
    {
        try
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var store = GetOrCreateSettingsStore();
                store.SetString(CodeSceneConstants.Titles.SETTINGSCOLLECTION, name, value);
            });
        }
        catch (Exception e)
        {
            _logger?.Error($"Failed to store provider key in settings store.", e);
        }
    }

    public void Delete(string name)
    {
        try
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var store = GetOrCreateSettingsStore();
                if (store.PropertyExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION, name))
                {
                    store.DeleteProperty(CodeSceneConstants.Titles.SETTINGSCOLLECTION, name);
                }
            });
        }
        catch (Exception e)
        {
            _logger?.Error($"Failed to delete provider key from settings store.", e);
        }
    }

    private WritableSettingsStore GetOrCreateSettingsStore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        if (!store.CollectionExists(CodeSceneConstants.Titles.SETTINGSCOLLECTION))
        {
            store.CreateCollection(CodeSceneConstants.Titles.SETTINGSCOLLECTION);
        }

        return store;
    }
}
