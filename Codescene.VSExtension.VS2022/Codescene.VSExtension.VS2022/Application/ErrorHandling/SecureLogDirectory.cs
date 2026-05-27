// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

internal static class SecureLogDirectory
{
    internal static void EnsureExists(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            return;
        }

        if (Directory.Exists(directoryPath))
        {
            TryApplyOwnerOnlyAccess(directoryPath);
            return;
        }

        var ownerSid = WindowsIdentity.GetCurrent().User;
        if (ownerSid == null)
        {
            Directory.CreateDirectory(directoryPath);
            return;
        }

        var security = CreateOwnerOnlyDirectorySecurity(ownerSid);
        Directory.CreateDirectory(directoryPath, security);
    }

    private static DirectorySecurity CreateOwnerOnlyDirectorySecurity(SecurityIdentifier ownerSid)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(
            ownerSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        return security;
    }

    private static void TryApplyOwnerOnlyAccess(string directoryPath)
    {
        try
        {
            var ownerSid = WindowsIdentity.GetCurrent().User;
            if (ownerSid == null)
            {
                return;
            }

            var directoryInfo = new DirectoryInfo(directoryPath);
            directoryInfo.SetAccessControl(CreateOwnerOnlyDirectorySecurity(ownerSid));
        }
        catch (Exception)
        {
        }
    }
}
