// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal static class ExperimentalInstanceDeploy
{
    internal static void EnsureExperimentalInstanceClosed()
    {
        foreach (var process in Process.GetProcessesByName("devenv"))
        {
            try
            {
                if (process.MainWindowTitle.IndexOf("experimental instance", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                process.Kill();
                process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    internal static void ResetExperimentalHive()
    {
        DeleteExperimentalHiveDirectories(Environment.SpecialFolder.LocalApplicationData);
        DeleteExperimentalHiveDirectories(Environment.SpecialFolder.ApplicationData);
    }

    internal static void DeployVsixToExperimentalInstance(string vsixPath, string devenvPath)
    {
        var (publisher, identityId, version) = ReadVsixIdentity(vsixPath);
        var hiveRoot = ResolveExperimentalHiveRoot();
        var extensionRoot = Path.Combine(
            hiveRoot,
            "Extensions",
            SanitizePathSegment(publisher),
            SanitizePathSegment(identityId),
            SanitizePathSegment(version));
        Directory.CreateDirectory(extensionRoot);
        ExtractVsix(vsixPath, extensionRoot);
        TouchExtensionsConfigurationChanged(hiveRoot);
        var vsInstanceId = Path.GetFileName(
            hiveRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        WriteExtensionEnabledInRegistry(vsInstanceId, identityId);

        var updateExit = TestProcessRunner.RunExternalProcess(
            devenvPath,
            $"/rootSuffix {E2ETestEnvironment.RootSuffix} /updateconfiguration",
            allowFailure: false,
            E2ETestEnvironment.UpdateConfigurationTimeout);
        Assert.AreEqual(0, updateExit, "devenv /updateconfiguration failed after manual VSIX extract.");

        TryDeleteDirectory(Path.Combine(hiveRoot, "ComponentModelCache"));
    }

    internal static void SuppressCopilotFreePromoNotification()
    {
        var hiveRoot = ResolveExperimentalHiveRoot();
        var vsHiveId = Path.GetFileName(hiveRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var keyPath = $@"Software\Microsoft\VisualStudio\{vsHiveId}\Notifications";
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key?.SetValue("CopilotFreePromo", 1, RegistryValueKind.DWord);
    }

    private static void DeleteExperimentalHiveDirectories(Environment.SpecialFolder folder)
    {
        var visualStudioDirectory = Path.Combine(
            Environment.GetFolderPath(folder),
            "Microsoft",
            "VisualStudio");
        if (!Directory.Exists(visualStudioDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(visualStudioDirectory, "*Exp", SearchOption.TopDirectoryOnly))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private static void WriteExtensionEnabledInRegistry(string vsInstanceId, string extensionId)
    {
        var keyPath = $@"Software\Microsoft\VisualStudio\{vsInstanceId}\ExtensionManager\EnabledExtensions";
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key?.SetValue(extensionId, 1, RegistryValueKind.DWord);
    }

    private static void TouchExtensionsConfigurationChanged(string experimentalHiveRoot)
    {
        var extensionsRoot = Path.Combine(experimentalHiveRoot, "Extensions");
        Directory.CreateDirectory(extensionsRoot);
        var marker = Path.Combine(extensionsRoot, "extensions.configurationchanged");
        if (!File.Exists(marker))
        {
            File.WriteAllBytes(marker, Array.Empty<byte>());
        }
        else
        {
            File.SetLastWriteTimeUtc(marker, DateTime.UtcNow);
        }
    }

    private static string ResolveExperimentalHiveRoot()
    {
        var visualStudioDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "VisualStudio");
        if (!Directory.Exists(visualStudioDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Could not find '{visualStudioDirectory}'. Launch Visual Studio 2022 at least once before running e2e tests.");
        }

        var candidates = Directory.EnumerateDirectories(visualStudioDirectory, "17.0_*")
            .Where(path => !path.EndsWith("Exp", StringComparison.OrdinalIgnoreCase))
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new DirectoryNotFoundException(
                $"Could not find a Visual Studio 17.0 instance folder under '{visualStudioDirectory}'.");
        }

        return candidates[0].FullName + "Exp";
    }

    private static (string Publisher, string IdentityId, string Version) ReadVsixIdentity(string vsixPath)
    {
        using var archive = ZipFile.OpenRead(vsixPath);
        var manifestEntry = archive.GetEntry("extension.vsixmanifest")
            ?? throw new InvalidOperationException($"VSIX '{vsixPath}' does not contain extension.vsixmanifest.");
        using var stream = manifestEntry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var metadata = document.Root?.Element(ns + "Metadata")
            ?? throw new InvalidOperationException("VSIX manifest is missing Metadata.");
        var identity = metadata.Element(ns + "Identity")
            ?? throw new InvalidOperationException("VSIX manifest is missing Identity.");
        var publisher = RequireIdentityAttribute(identity, "Publisher");
        var identityId = RequireIdentityAttribute(identity, "Id");
        var version = RequireIdentityAttribute(identity, "Version");
        return (publisher, identityId, version);
    }

    private static string RequireIdentityAttribute(XElement identity, string attributeName)
    {
        var value = identity.Attribute(attributeName)?.Value.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"VSIX Identity is missing attribute '{attributeName}'.");
        }

        return value!;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private static void ExtractVsix(string vsixPath, string targetDirectory)
    {
        var fullTarget = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(vsixPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, relative));
            if (!destinationPath.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"VSIX entry '{entry.FullName}' would extract outside the extension directory.");
            }

            var parent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
