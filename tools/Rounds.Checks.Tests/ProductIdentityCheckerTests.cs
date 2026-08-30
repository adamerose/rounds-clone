using Rounds.Checks;

namespace Rounds.Checks.Tests;

public sealed class ProductIdentityCheckerTests : IDisposable
{
    private readonly string _fixture = Path.Combine(
        Path.GetTempPath(),
        $"rounds-identity-check-{Guid.NewGuid():N}");

    [Fact]
    public void CurrentRepositoryPassesIdentityBoundary()
    {
        Assert.Empty(ProductIdentityChecker.CheckRepository(FindRepository()));
    }

    [Fact]
    public void SupersededProductTitleOnActiveSurfaceIsRejected()
    {
        CopyIdentityFixture();
        var readme = Path.Combine(_fixture, "README.md");
        File.AppendAllText(readme, "\nRICOCHET\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN002 README.md", StringComparison.Ordinal));
    }

    [Fact]
    public void ChangedSchemaIdAndInventedSupportedCardNameAreRejected()
    {
        CopyIdentityFixture();
        var schema = Path.Combine(_fixture, "spec", "schema", "cards.schema.json");
        File.WriteAllText(schema, File.ReadAllText(schema).Replace(
            "https://ricochet.local/schema/cards.schema.json",
            "https://rounds.invalid/schema/cards.schema.json",
            StringComparison.Ordinal));
        var catalog = Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardCatalog.cs");
        File.WriteAllText(catalog, File.ReadAllText(catalog).Replace(
            "\"bouncy\" => \"Bouncy\"",
            "\"bouncy\" => \"Rebound\"",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN003", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("IDN009 supported card `bouncy`", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeCardSummaryMembersAreRejected()
    {
        CopyIdentityFixture();
        var definition = Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardDefinition.cs");
        InsertBeforeFinalBrace(definition, "\n    public string Summary { get; } = string.Empty;\n");
        var catalog = Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardCatalog.cs");
        InsertBeforeFinalBrace(catalog, "\n    private static string SummaryFor(string id) => id;\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN011", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("IDN012", StringComparison.Ordinal));
    }

    [Fact]
    public void ArbitraryMainSourceByteChangeRequiresLiveUiReview()
    {
        CopyIdentityFixture();
        var main = Path.Combine(_fixture, "game", "Main.cs");
        File.AppendAllText(main, "\n// reviewed live UI changed\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure ==
            "IDN010 game/Main.cs changed from the reviewed live Main UI source; " +
            "deliberately review the complete live UI boundary and update ExpectedMainSourceSha256.");
    }

    [Theory]
    [InlineData("private void RefAliasBypass() { string text = string.Empty; ref string alias = ref text; alias = _world.Phase.ToString(); }")]
    [InlineData("private void DeconstructionBypass() { var text = string.Empty; (text, _) = (_world.Phase.ToString(), 0); }")]
    [InlineData("private void OutlineBypass() { DrawStringOutline(ThemeDB.FallbackFont, Vector2.Zero, _world.Phase.ToString()); }")]
    [InlineData("private void MultilineBypass() { DrawMultilineString(ThemeDB.FallbackFont, Vector2.Zero, _world.Phase.ToString()); }")]
    public void RepresentativeAlternateTextFlowsRequireLiveUiReview(string mutation)
    {
        CopyIdentityFixture();
        var main = Path.Combine(_fixture, "game", "Main.cs");
        InsertBeforeFinalBrace(main, $"\n    {mutation}\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void SummaryWordsInCommentsDoNotCreateRuntimeSummaryFailures()
    {
        CopyIdentityFixture();
        File.AppendAllText(Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardDefinition.cs"),
            "\n// Summary\n");
        File.AppendAllText(Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardCatalog.cs"),
            "\n// SummaryFor\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.DoesNotContain(failures, failure =>
            failure.StartsWith("IDN011", StringComparison.Ordinal) ||
            failure.StartsWith("IDN012", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_fixture))
        {
            Directory.Delete(_fixture, recursive: true);
        }
    }

    private void CopyIdentityFixture()
    {
        var repository = FindRepository();
        foreach (var relativePath in new[]
        {
            "GOAL.md",
            "README.md",
            "game/project.godot",
            "game/Main.cs",
            "docs/architecture.md",
            "docs/design/visual-system.md",
            "research/notes/core-rules.md",
            "spec/cards.json",
            "spec/schema/cards.schema.json",
            "spec/schema/maps.schema.json",
            "spec/schema/measurements.schema.json",
            "spec/schema/mechanics.schema.json",
            "spec/schema/source-index.schema.json",
            "src/Rounds.Sim/Cards/StatCardDefinition.cs",
            "src/Rounds.Sim/Cards/StatCardCatalog.cs",
        })
        {
            var destination = Path.Combine(_fixture, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(repository, relativePath.Replace('/', Path.DirectorySeparatorChar)), destination);
        }
    }

    private static void InsertBeforeFinalBrace(string path, string insertion)
    {
        var source = File.ReadAllText(path);
        var finalBrace = source.LastIndexOf('}');
        Assert.True(finalBrace >= 0);
        File.WriteAllText(path, source.Insert(finalBrace, insertion));
    }

    private static string FindRepository()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Rounds.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
