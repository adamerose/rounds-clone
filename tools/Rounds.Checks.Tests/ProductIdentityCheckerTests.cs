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

    [Theory]
    [InlineData("Convert.ToString(_world.Phase)", false)]
    [InlineData("$\"{_world.Phase}\"", false)]
    [InlineData("Convert.ToString(_world.Players[0].AimDirection.X)", true)]
    [InlineData("_world.Arena.Id", false)]
    [InlineData("Convert.ToString(_world.Bullets.Count)", false)]
    [InlineData("Convert.ToString(_world.Bullets[0].BouncesRemaining)", true)]
    [InlineData("Convert.ToString(_world.Players[0].BlockPhase)", false)]
    [InlineData("Convert.ToString(_world.Players[0].BlockTicksRemaining)", true)]
    public void AlternateCompilableLiveTextFlowsAreRejected(string expression, bool throughAlias)
    {
        CopyIdentityFixture();
        var main = Path.Combine(_fixture, "game", "Main.cs");
        var source = File.ReadAllText(main);
        var marker = "    private readonly record struct CameraTransform";
        var rendered = throughAlias ? "rendered" : expression;
        var alias = throughAlias ? $"        var rendered = {expression};\n" : string.Empty;
        var method = $$"""
            private void DrawUnsupportedFixture()
            {
        {{alias}}        DrawString(
                    ThemeDB.FallbackFont,
                    Vector2.Zero,
                    {{rendered}},
                    HorizontalAlignment.Left,
                    200.0f,
                    16,
                    Paper);
            }

        """;
        Assert.Contains(marker, source, StringComparison.Ordinal);
        File.WriteAllText(main, source.Replace(marker, method + marker, StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void DisplayNameMustStillComeFromTheDraftCardDefinition()
    {
        CopyIdentityFixture();
        var main = Path.Combine(_fixture, "game", "Main.cs");
        File.WriteAllText(main, File.ReadAllText(main).Replace(
            "var card = match.CurrentOffer[index];",
            "var card = new { DisplayName = Convert.ToString(_world.Phase) };",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void ForbiddenWordsInCommentsDoNotCreateLiveTextOrRuntimeSummaryFailures()
    {
        CopyIdentityFixture();
        File.AppendAllText(Path.Combine(_fixture, "game", "Main.cs"),
            "\n// DrawString card.Summary BLOCK READY _world.Phase AimDirection.X Arena.Id Bullets.Count\n");
        File.AppendAllText(Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardDefinition.cs"),
            "\n// Summary\n");
        File.AppendAllText(Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardCatalog.cs"),
            "\n// SummaryFor\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.DoesNotContain(failures, failure =>
            failure.StartsWith("IDN010", StringComparison.Ordinal) ||
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
