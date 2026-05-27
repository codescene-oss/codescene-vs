// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Reflection;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ExtensionExceptionFilterTests
{
    [TestMethod]
    public void IsFromExtension_MessageContainingNamespaceWithoutExtensionFrames_ReturnsFalse()
    {
        var ex = new Exception("Codescene.VSExtension fake");

        Assert.IsFalse(ExtensionExceptionFilter.IsFromExtension(ex));
    }

    [TestMethod]
    public void IsFromExtension_ThrownFromExtensionCode_ReturnsTrue()
    {
        try
        {
            ThrowFromExtensionCode();
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ExtensionExceptionFilter.IsFromExtension(ex));
        }
    }

    [TestMethod]
    public void IsFromExtension_WrappedInnerExceptionFromExtension_ReturnsTrue()
    {
        var ex = new Exception("wrapper", CaptureExtensionException());

        Assert.IsTrue(ExtensionExceptionFilter.IsFromExtension(ex));
    }

    [TestMethod]
    public void IsFromExtension_StackTraceWithExtensionFrameWhenTargetSiteUnset_ReturnsTrue()
    {
        var ex = CaptureExtensionException();
        ClearExceptionTargetSite(ex);

        Assert.IsTrue(ExtensionExceptionFilter.IsFromExtension(ex));
    }

    private static Exception CaptureExtensionException()
    {
        try
        {
            ThrowFromExtensionCode();
            throw new InvalidOperationException("Expected extension exception");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void ThrowFromExtensionCode() => throw new InvalidOperationException("test");

    private static void ClearExceptionTargetSite(Exception ex)
    {
        var targetSiteField = typeof(Exception).GetField(
            "_exceptionMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(targetSiteField);
        targetSiteField.SetValue(ex, null);
    }
}
