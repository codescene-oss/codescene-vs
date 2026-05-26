// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class WorkspaceFilePathValidatorTests
{
    private string _workspaceDir;
    private string _outsideDir;

    [TestInitialize]
    public void Setup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ws-path-validator-{Guid.NewGuid()}");
        _workspaceDir = Path.Combine(tempDir, "workspace");
        _outsideDir = Path.Combine(tempDir, "outside");
        Directory.CreateDirectory(_workspaceDir);
        Directory.CreateDirectory(_outsideDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var root = Directory.GetParent(_workspaceDir)?.FullName;
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void HasUnsafePathSyntax_ParentTraversal_ReturnsTrue()
    {
        Assert.IsTrue(WorkspaceFilePathValidator.HasUnsafePathSyntax(@"C:\repo\..\secret.cs"));
    }

    [TestMethod]
    public void HasUnsafePathSyntax_UncPath_ReturnsTrue()
    {
        Assert.IsTrue(WorkspaceFilePathValidator.HasUnsafePathSyntax(@"\\server\share\file.cs"));
    }

    [TestMethod]
    public void HasUnsafePathSyntax_ShellMetacharacter_ReturnsTrue()
    {
        Assert.IsTrue(WorkspaceFilePathValidator.HasUnsafePathSyntax(@"C:\repo\file|other.cs"));
    }

    [TestMethod]
    public void HasUnsafePathSyntax_AlternateDataStream_ReturnsTrue()
    {
        Assert.IsTrue(WorkspaceFilePathValidator.HasUnsafePathSyntax(@"C:\repo\file.cs:stream"));
    }

    [TestMethod]
    public void HasUnsafePathSyntax_ValidFileName_ReturnsFalse()
    {
        Assert.IsFalse(WorkspaceFilePathValidator.HasUnsafePathSyntax(@"C:\repo\file..cs"));
    }

    [TestMethod]
    public void IsAllowedWorkspaceFilePath_NullRoots_ReturnsFalse()
    {
        var filePath = Path.Combine(_workspaceDir, "file.cs");
        Assert.IsFalse(WorkspaceFilePathValidator.IsAllowedWorkspaceFilePath(filePath, null));
    }

    [TestMethod]
    public void IsAllowedWorkspaceFilePath_PathUnderWorkspace_ReturnsTrue()
    {
        var filePath = Path.Combine(_workspaceDir, "file.cs");
        File.WriteAllText(filePath, "x");
        Assert.IsTrue(WorkspaceFilePathValidator.IsAllowedWorkspaceFilePath(filePath, new[] { _workspaceDir }));
    }

    [TestMethod]
    public void IsAllowedWorkspaceFilePath_PathOutsideWorkspace_ReturnsFalse()
    {
        var filePath = Path.Combine(_outsideDir, "file.cs");
        File.WriteAllText(filePath, "x");
        Assert.IsFalse(WorkspaceFilePathValidator.IsAllowedWorkspaceFilePath(filePath, new[] { _workspaceDir }));
    }

    [TestMethod]
    public void IsAllowedWorkspaceFilePath_RelativePathUnderWorkspace_ReturnsTrue()
    {
        var subDir = Path.Combine(_workspaceDir, "src");
        Directory.CreateDirectory(subDir);
        var filePath = Path.Combine(subDir, "file.cs");
        File.WriteAllText(filePath, "x");
        Assert.IsTrue(WorkspaceFilePathValidator.IsAllowedWorkspaceFilePath(
            "src" + Path.DirectorySeparatorChar + "file.cs",
            new[] { _workspaceDir }));
    }
}
