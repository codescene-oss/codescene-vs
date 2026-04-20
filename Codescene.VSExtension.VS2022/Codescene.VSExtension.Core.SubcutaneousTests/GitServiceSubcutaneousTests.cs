// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.SubcutaneousTests;

[TestClass]
public class GitServiceSubcutaneousTests : SubcutaneousGitTestBase
{
    private const string MainContent = @"namespace TestingCodeSceneVS.CSharp
{
    internal class TesfFile
    {
        public void TestMethod(string t1)
        {
            Console.WriteLine(""Hello world"");
        }
    }
}";

    private const string DevelopContent = @"namespace TestingCodeSceneVS.CSharp
{
    internal class TesfFile
    {
        public void TestMethod(string t1)
        {
            Console.WriteLine(""Hello world"");
        }
        public void Hello(string t1, string t2, string t3, string t4, string t5)
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

    private const string FeatureContent = @"namespace TestingCodeSceneVS.CSharp
{
    internal class TesfFile
    {
        public void TestMethod(string t1)
        {
            Console.WriteLine(""Hello world"");
        }
        public void Hello(string t1, string t2, string t3, string t4, string t5)
        {
            Console.WriteLine(""Hello"");
            if (true) { if (true) { if (true) { if (true) { if (true) { } } } } }
        }
    }
}";

    protected override bool AutoStartObserver => false;

    [TestMethod]
    public async Task GetFileContentForCommit_OnFeatureBranchBasedOnDevelop_UsesDevelopBaseline()
    {
        const string relativePath = "src/TesfFile.cs";

        await CreateCommittedFileAsync(relativePath, MainContent, "Add baseline file on main");

        CheckoutBranch("develop", create: true);
        await WriteWorkingFileAsync(relativePath, DevelopContent);
        CommitAll("Improve baseline file on develop");

        string developCommit;
        using (var repo = new Repository(RepositoryRoot))
        {
            developCommit = repo.Head.Tip.Sha;
        }

        CheckoutBranch("feature/test-suppression2", create: true);
        await WriteWorkingFileAsync(relativePath, FeatureContent);

        using (var repo = new Repository(RepositoryRoot))
        {
            var baselineCommit = GitService.GetBaselineCommit(repo);
            Assert.AreEqual(developCommit, baselineCommit);
        }

        var baselineContent = GitService.GetFileContentForCommit(AbsolutePath(relativePath));
        Assert.AreEqual(NormalizeLineEndings(DevelopContent), NormalizeLineEndings(baselineContent));
        Assert.AreNotEqual(NormalizeLineEndings(MainContent), NormalizeLineEndings(baselineContent));
    }

    [TestMethod]
    public async Task ReviewWithDeltaAsync_OnFeatureBranchBasedOnDevelop_UsesDevelopBaselineForActualCliScores()
    {
        const string relativePath = "src/TesfFile.cs";

        var absolutePath = await CreateCommittedFileAsync(relativePath, MainContent, "Add baseline file on main");
        var mainReview = await CodeReviewer.ReviewAsync(absolutePath, MainContent);
        Assert.AreEqual(10.00m, RoundScore(mainReview.Score));

        CheckoutBranch("develop", create: true);
        await WriteWorkingFileAsync(relativePath, DevelopContent);
        CommitAll("Improve baseline file on develop");

        var developReview = await CodeReviewer.ReviewAsync(absolutePath, DevelopContent);
        Assert.AreEqual(9.68m, RoundScore(developReview.Score));

        CheckoutBranch("feature/test-suppression2", create: true);
        await WriteWorkingFileAsync(relativePath, FeatureContent);

        var baselineContent = GitService.GetFileContentForCommit(absolutePath);
        Assert.AreEqual(NormalizeLineEndings(DevelopContent), NormalizeLineEndings(baselineContent));

        var (featureReview, delta) = await CodeReviewer.ReviewWithDeltaAsync(absolutePath, FeatureContent);

        Assert.AreEqual(9.09m, RoundScore(featureReview.Score));
        Assert.IsNotNull(delta);
        Assert.AreEqual(9.68m, RoundScore(delta.OldScore));
        Assert.AreEqual(9.09m, RoundScore(delta.NewScore));
        Assert.AreEqual(-0.59m, RoundScore(delta.ScoreChange));
    }

    private static decimal RoundScore(float score)
    {
        return decimal.Round(System.Convert.ToDecimal(score), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundScore(decimal score)
    {
        return decimal.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
