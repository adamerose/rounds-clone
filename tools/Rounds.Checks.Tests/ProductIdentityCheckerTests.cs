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
    public void BaseProjectileKeepsBrightCoreAndOwnerTrailWithoutDarkRing()
    {
        var source = File.ReadAllText(Path.Combine(FindRepository(), "game", "Main.cs"));
        var start = source.IndexOf("private void DrawBullet", StringComparison.Ordinal);
        var end = source.IndexOf("private void DrawHud", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var drawBullet = source[start..end];
        Assert.Contains("color with { A = 0.45f }", drawBullet, StringComparison.Ordinal);
        Assert.Contains("DrawCircle(center, radius, Paper);", drawBullet, StringComparison.Ordinal);
        Assert.DoesNotContain("Ink", drawBullet, StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoredWorkspaceToolCacheDoesNotChangeRuntimeBoundary()
    {
        CopyIdentityFixture();
        var cache = Path.Combine(_fixture, ".tools", "sdk", "nested");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "Injected.targets"), "not repository-controlled");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.DoesNotContain(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
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
            "IDN010 the complete shipped runtime/build boundary changed; " +
            "deliberately review every included input and update ExpectedShippedRuntimeBoundarySha256.");
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
    public void MainSceneChildLabelRequiresLivePresentationReview()
    {
        CopyIdentityFixture();
        File.AppendAllText(
            Path.Combine(_fixture, "game", "Main.tscn"),
            "\n[node name=\"UnreviewedCopy\" type=\"Label\" parent=\".\"]\ntext = \"MATCH PHASE\"\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void NewGameScriptRequiresLivePresentationReview()
    {
        CopyIdentityFixture();
        File.WriteAllText(
            Path.Combine(_fixture, "game", "UnreviewedLabel.cs"),
            "public sealed class UnreviewedLabel { public const string Text = \"MATCH PHASE\"; }");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void RootBuildInjectionRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        var buildProps = Path.Combine(_fixture, "Directory.Build.props");
        File.WriteAllText(
            buildProps,
            File.ReadAllText(buildProps).Replace(
                "</Project>",
            """
              <ItemGroup Condition="'$(MSBuildProjectName)' == 'Rounds.Game'">
                <Compile Include="$(MSBuildThisFileDirectory)InjectedMain.cs" Link="InjectedMain.cs" />
              </ItemGroup>
            </Project>
            """,
                StringComparison.Ordinal));
        File.WriteAllText(
            Path.Combine(_fixture, "InjectedMain.cs"),
            """
            using Godot;
            namespace Rounds.Game;
            public sealed partial class Main
            {
                public override void _EnterTree()
                {
                    AddChild(new Label { Text = "MATCH PHASE" });
                }
            }
            """);

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void NewRootSourceRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        File.WriteAllText(
            Path.Combine(_fixture, "InjectedMain.cs"),
            "namespace Rounds.Game; public sealed partial class Main { }");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void NewRepositoryBuildControlRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        var buildDirectory = Path.Combine(_fixture, "eng");
        Directory.CreateDirectory(buildDirectory);
        File.WriteAllText(Path.Combine(buildDirectory, "Injected.targets"), "<Project />");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void SimulationSourceMutationRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        File.AppendAllText(Path.Combine(_fixture, "src", "Rounds.Sim", "MatchPhase.cs"), "\n// changed\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void EmbeddedCombatSpecMutationRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        var combat = Path.Combine(_fixture, "spec", "combat.json");
        File.WriteAllText(combat, File.ReadAllText(combat).Replace(
            "\"value\": 0.38",
            "\"value\": 0.39",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void CardSpecMutationRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        var cards = Path.Combine(_fixture, "spec", "cards.json");
        File.AppendAllText(cards, "\n");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void NewReplaySourceRequiresRuntimeBoundaryReview()
    {
        CopyIdentityFixture();
        File.WriteAllText(
            Path.Combine(_fixture, "src", "Rounds.Replay", "InjectedReplay.cs"),
            "namespace Rounds.Replay; public static class InjectedReplay { }");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void DeletedGameSourceRequiresLivePresentationReview()
    {
        CopyIdentityFixture();
        File.Delete(Path.Combine(_fixture, "game", "StartupRoute.cs"));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void IncompleteFidelityCopyMutationRequiresLivePresentationReview()
    {
        CopyIdentityFixture();
        var shell = Path.Combine(_fixture, "game", "FaithfulSubsetMatchShell.cs");
        File.WriteAllText(shell, File.ReadAllText(shell).Replace(
            "THE OPENING CARDS AND FIRST FULL ROUND ARE THE CURRENT PLAYABLE SUBSET",
            "UNREVIEWED SUBSTITUTE COPY",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void CardDisplayNameProviderMutationRequiresLivePresentationReview()
    {
        CopyIdentityFixture();
        var definition = Path.Combine(_fixture, "src", "Rounds.Sim", "Cards", "StatCardDefinition.cs");
        File.WriteAllText(definition, File.ReadAllText(definition).Replace(
            "DisplayName = displayName;",
            "DisplayName = id;",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Fact]
    public void DraftOfferMustResolveItsDisplayNameThroughTheCanonicalCatalog()
    {
        CopyIdentityFixture();
        var main = Path.Combine(_fixture, "game", "Main.cs");
        File.WriteAllText(main, File.ReadAllText(main).Replace(
            "_displayCards.GetRequired(card.Id).DisplayName",
            "card.DisplayName",
            StringComparison.Ordinal));

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.Contains(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("game", ".godot")]
    [InlineData("game", "bin")]
    [InlineData("game", "obj")]
    [InlineData("src/Rounds.Sim", ".godot")]
    [InlineData("src/Rounds.Sim", "bin")]
    [InlineData("src/Rounds.Sim", "obj")]
    [InlineData("src/Rounds.Replay", ".godot")]
    [InlineData("src/Rounds.Replay", "bin")]
    [InlineData("src/Rounds.Replay", "obj")]
    public void GeneratedRuntimeDirectoryFilesDoNotChangeRuntimeBoundary(string root, string directory)
    {
        CopyIdentityFixture();
        var generatedDirectory = Path.Combine(
            _fixture,
            root.Replace('/', Path.DirectorySeparatorChar),
            directory,
            "nested");
        Directory.CreateDirectory(generatedDirectory);
        File.WriteAllText(Path.Combine(generatedDirectory, "generated.txt"), "not shipped source");

        var failures = ProductIdentityChecker.CheckRepository(_fixture);

        Assert.DoesNotContain(failures, failure => failure.StartsWith("IDN010", StringComparison.Ordinal));
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
        CopyRuntimeBoundary(repository);
        foreach (var relativePath in new[]
        {
            "GOAL.md",
            "README.md",
            "docs/architecture.md",
            "docs/design/visual-system.md",
            "research/notes/core-rules.md",
        })
        {
            var destination = Path.Combine(_fixture, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(repository, relativePath.Replace('/', Path.DirectorySeparatorChar)), destination);
        }
    }

    private void CopyRuntimeBoundary(string repository)
    {
        foreach (var relativeRoot in new[] { "game", "spec", "src/Rounds.Sim", "src/Rounds.Replay" })
        {
            CopyRuntimeRoot(repository, relativeRoot);
        }

        foreach (var source in EnumerateRepositoryFiles(repository))
        {
            var extension = Path.GetExtension(source);
            var filename = Path.GetFileName(source);
            if (extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".rsp", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase))
            {
                CopyRepositoryFile(repository, source);
            }
        }

        foreach (var source in Directory.EnumerateFiles(repository, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(source) is var extension &&
                (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals(".fs", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals(".vb", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals(".csx", StringComparison.OrdinalIgnoreCase)))
            {
                CopyRepositoryFile(repository, source);
            }
        }
    }

    private void CopyRuntimeRoot(string repository, string relativeRoot)
    {
        var sourceRoot = Path.Combine(repository, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        var pending = new Stack<string>();
        pending.Push(sourceRoot);
        while (pending.TryPop(out var directory))
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                if (!IsExcludedGeneratedDirectory(childDirectory))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var source in Directory.EnumerateFiles(directory))
            {
                CopyRepositoryFile(repository, source);
            }
        }
    }

    private static IEnumerable<string> EnumerateRepositoryFiles(string repository)
    {
        var pending = new Stack<string>();
        pending.Push(repository);
        while (pending.TryPop(out var directory))
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(childDirectory);
                if (!name.Equals(".git", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(".ivy", StringComparison.OrdinalIgnoreCase) &&
                    !IsExcludedGeneratedDirectory(childDirectory))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }
        }
    }

    private void CopyRepositoryFile(string repository, string source)
    {
        var destination = Path.Combine(_fixture, Path.GetRelativePath(repository, source));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static bool IsExcludedGeneratedDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return name.Equals(".godot", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("obj", StringComparison.OrdinalIgnoreCase);
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
