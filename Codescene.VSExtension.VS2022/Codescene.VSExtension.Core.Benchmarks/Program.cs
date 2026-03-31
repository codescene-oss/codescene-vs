// Copyright (c) CodeScene. All rights reserved.

using BenchmarkDotNet.Running;

namespace Codescene.VSExtension.Core.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
