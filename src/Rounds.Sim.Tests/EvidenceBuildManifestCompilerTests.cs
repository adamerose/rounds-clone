using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Rounds.EvidenceLauncher;

namespace Rounds.Sim.Tests;

public sealed class EvidenceBuildManifestCompilerTests
{
    private const string RepositoryRoot = @"C:\evidence\rounds-clone";
    private const string AlternateRepositoryRoot = @"D:\other\rounds-clone";
    private const string PackageRoot = @"C:\evidence\packages";
    private const string AlternatePackageRoot = @"D:\other\packages";

    [Fact]
    public void Exact_manifest_is_order_independent_and_uses_immutable_sorted_records()
    {
        var fileBytes = new byte[] { 1, 2, 3 };
        var source = new List<EvidenceManifestEntry>
        {
            File("z/file.bin", fileBytes),
            Directory("z"),
        };
        var definition = new EvidenceManifestDefinition(source);
        fileBytes[0] = 9;
        source.Clear();

        var first = Compile(definition, [Directory("z"), File("z/file.bin", [1, 2, 3])]);
        var second = Compile(definition, [File("z/file.bin", [1, 2, 3]), Directory("z")]);

        Assert.Equal(EvidenceBuildManifestCompiler.Algorithm, first.Algorithm);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(new[] { "z", "z/file.bin" }, first.Entries.Select(value => value.RelativePath));
        Assert.IsType<ReadOnlyCollection<EvidenceCompiledManifestEntry>>(first.Entries);
        Assert.Equal("039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81", first.Entries[1].ContentSha256);
    }

    [Fact]
    public void Length_delimited_kind_path_transform_and_content_fields_do_not_alias()
    {
        var fileA = Compile(new EvidenceManifestDefinition([File("a", [])]), [File("a", [])]);
        var fileB = Compile(new EvidenceManifestDefinition([File("a", [0])]), [File("a", [0])]);
        var directory = Compile(new EvidenceManifestDefinition([Directory("a")]), [Directory("a")]);
        var splitPath = Compile(
            new EvidenceManifestDefinition([Directory("a"), File("b", [1])]),
            [Directory("a"), File("b", [1])]);
        var joinedPath = Compile(
            new EvidenceManifestDefinition([File("a/b", [1])]),
            [File("a/b", [1])]);

        Assert.Equal("42a37ee0f05cbc594e0bd8556a0fa99421b97e7b1af76284c13837d348065e0e", fileA.Sha256);
        Assert.Equal(5, new[] { fileA.Sha256, fileB.Sha256, directory.Sha256, splitPath.Sha256, joinedPath.Sha256 }
            .Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/root")]
    [InlineData("C:\\root")]
    [InlineData("a//b")]
    [InlineData("a/./b")]
    [InlineData("a/../b")]
    [InlineData("a/b.")]
    [InlineData("a/b ")]
    [InlineData("a:b")]
    [InlineData("a/*")]
    [InlineData("con.txt")]
    public void Relative_path_refuses_noncanonical_or_Windows_aliases(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => File(path, [1]));

    [Fact]
    public void Relative_path_normalizes_directory_separator_and_rejects_invalid_unicode()
    {
        Assert.Equal("a/b", File(@"a\b", [1]).RelativePath);
        Assert.Throws<ArgumentException>(() => File("a/\ud800", [1]));
    }

    [Fact]
    public void Definition_refuses_duplicate_case_collision_and_required_excluded_overlap()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceManifestDefinition([File("a", []), File("a", [])]));
        Assert.Throws<ArgumentException>(() => new EvidenceManifestDefinition([File("a", []), File("A", [])]));
        Assert.Throws<ArgumentException>(() => new EvidenceManifestDefinition([File("a", [])], ["A"]));
        Assert.Throws<ArgumentException>(() => new EvidenceManifestDefinition([File("a", [])], ["x", "X"]));
    }

    [Fact]
    public void Compile_refuses_duplicate_case_collision_missing_extra_excluded_and_kind_drift()
    {
        var definition = new EvidenceManifestDefinition([Directory("dir"), File("dir/file", [1])], ["excluded"]);
        Assert.Throws<ArgumentException>(() => Compile(definition, [Directory("dir"), File("dir/file", [1]), File("dir/file", [1])]));
        Assert.Throws<ArgumentException>(() => Compile(definition, [Directory("dir"), File("dir/file", [1]), File("DIR/FILE", [1])]));
        Assert.Throws<InvalidOperationException>(() => Compile(definition, [Directory("dir")]));
        Assert.Throws<InvalidOperationException>(() => Compile(definition, [Directory("dir"), File("dir/file", [1]), File("extra", [])]));
        Assert.Throws<InvalidOperationException>(() => Compile(definition, [Directory("dir"), File("excluded", [])]));
        Assert.Throws<InvalidOperationException>(() => Compile(definition, [Directory("dir"), Directory("dir/file")]));
    }

    [Fact]
    public void Directory_refuses_content_and_transform()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceManifestEntry("d", EvidenceManifestRecordKind.Directory, [1]));
        Assert.Throws<ArgumentException>(() => new EvidenceManifestEntry(
            "d", EvidenceManifestRecordKind.Directory, [],
            EvidenceManifestContentKind.RepositoryAndPackageRootNormalizedProjectAssetsJson));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvidenceManifestEntry(
            "d", (EvidenceManifestRecordKind)9, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvidenceManifestEntry(
            "d", EvidenceManifestRecordKind.File, [], (EvidenceManifestContentKind)9));
    }

    [Fact]
    public void Manifest_roots_must_be_canonical_absolute_and_distinct()
    {
        var definition = new EvidenceManifestDefinition([File("a", [])]);
        Assert.Throws<ArgumentException>(() => EvidenceBuildManifestCompiler.CompileExact(definition, [File("a", [])], "relative", PackageRoot));
        Assert.Throws<ArgumentException>(() => EvidenceBuildManifestCompiler.CompileExact(definition, [File("a", [])], RepositoryRoot + "\\", PackageRoot));
        Assert.Throws<ArgumentException>(() => EvidenceBuildManifestCompiler.CompileExact(definition, [File("a", [])], RepositoryRoot, RepositoryRoot.ToUpperInvariant()));
        Assert.Throws<ArgumentException>(() => EvidenceBuildManifestCompiler.CompileExact(definition, [File("a", [])], RepositoryRoot + ":ads", PackageRoot));
    }

    [Fact]
    public void Project_assets_normalization_is_deterministic_across_exact_roots()
    {
        var first = ProjectAssets(
            RepositoryRoot,
            PackageRoot,
            extraProperty: "unchanged",
            reverseProperties: false);
        var second = ProjectAssets(
            AlternateRepositoryRoot,
            AlternatePackageRoot,
            extraProperty: "unchanged",
            reverseProperties: true);
        var definition = new EvidenceManifestDefinition([NormalizedJson("obj/project.assets.json", first)]);
        var alternateDefinition = new EvidenceManifestDefinition([NormalizedJson("obj/project.assets.json", second)]);

        var firstCompiled = EvidenceBuildManifestCompiler.CompileExact(
            definition, [NormalizedJson("obj/project.assets.json", first)], RepositoryRoot, PackageRoot);
        var secondCompiled = EvidenceBuildManifestCompiler.CompileExact(
            alternateDefinition, [NormalizedJson("obj/project.assets.json", second)], AlternateRepositoryRoot, AlternatePackageRoot);

        Assert.Equal(firstCompiled.Sha256, secondCompiled.Sha256);
        Assert.Equal(firstCompiled.Entries[0].ContentSha256, secondCompiled.Entries[0].ContentSha256);
    }

    [Fact]
    public void Project_assets_normalizes_only_narrow_named_path_fields()
    {
        var first = ProjectAssets(RepositoryRoot, PackageRoot, RepositoryRoot, reverseProperties: false);
        var second = ProjectAssets(AlternateRepositoryRoot, AlternatePackageRoot, AlternateRepositoryRoot, reverseProperties: false);
        var firstNormalized = EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(first, RepositoryRoot, PackageRoot);
        var secondNormalized = EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(second, AlternateRepositoryRoot, AlternatePackageRoot);

        Assert.NotEqual(firstNormalized, secondNormalized);
        using var normalized = JsonDocument.Parse(firstNormalized);
        Assert.Equal(RepositoryRoot, normalized.RootElement.GetProperty("unrelated").GetString());
        Assert.Equal("${REPOSITORY_ROOT}/game/Rounds.Game.csproj",
            normalized.RootElement.GetProperty("project").GetProperty("restore").GetProperty("projectPath").GetString());
        Assert.True(normalized.RootElement.GetProperty("packageFolders").TryGetProperty("${PACKAGE_ROOT}/", out _));
    }

    [Theory]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"a\":1,\"A\":2}")]
    [InlineData("{\"a\":/*comment*/1}")]
    [InlineData("{\"a\":1,}")]
    [InlineData("[]")]
    public void Project_assets_refuses_duplicate_case_collision_comments_trailing_and_nonobject(string json) =>
        Assert.ThrowsAny<Exception>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(
            Encoding.UTF8.GetBytes(json), RepositoryRoot, PackageRoot));

    [Fact]
    public void Project_assets_refuses_BOM_and_invalid_UTF8()
    {
        var valid = Encoding.UTF8.GetBytes("{}");
        Assert.Throws<InvalidDataException>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(
            Encoding.UTF8.GetPreamble().Concat(valid).ToArray(), RepositoryRoot, PackageRoot));
        Assert.Throws<DecoderFallbackException>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(
            [0xff, 0xfe], RepositoryRoot, PackageRoot));
    }

    [Fact]
    public void Project_assets_refuses_named_repository_escape_and_package_root_drift()
    {
        var escaped = JsonSerializer.SerializeToUtf8Bytes(new
        {
            project = new { restore = new { projectPath = @"C:\outside\project.csproj" } },
        });
        var packageDrift = JsonSerializer.SerializeToUtf8Bytes(new
        {
            project = new { restore = new { packagesPath = @"C:\outside\packages" } },
        });
        var folderDrift = Encoding.UTF8.GetBytes("{\"packageFolders\":{\"C:\\\\outside\\\\packages\\\\\":{}}}");

        Assert.Throws<InvalidDataException>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(escaped, RepositoryRoot, PackageRoot));
        Assert.Throws<InvalidDataException>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(packageDrift, RepositoryRoot, PackageRoot));
        Assert.Throws<InvalidDataException>(() => EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(folderDrift, RepositoryRoot, PackageRoot));
    }

    [Fact]
    public void Project_assets_does_not_normalize_same_named_field_outside_admitted_structure()
    {
        var first = JsonSerializer.SerializeToUtf8Bytes(new
        {
            untrusted = new { projectPath = RepositoryRoot + @"\game\Rounds.Game.csproj" },
        });
        var second = JsonSerializer.SerializeToUtf8Bytes(new
        {
            untrusted = new { projectPath = AlternateRepositoryRoot + @"\game\Rounds.Game.csproj" },
        });

        var firstNormalized = EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(first, RepositoryRoot, PackageRoot);
        var secondNormalized = EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(second, AlternateRepositoryRoot, AlternatePackageRoot);

        Assert.NotEqual(firstNormalized, secondNormalized);
    }

    [Fact]
    public void Project_assets_property_names_cannot_spoof_an_admitted_JSON_path()
    {
        var path = RepositoryRoot + @"\game\Rounds.Game.csproj";
        var json = Encoding.UTF8.GetBytes($"{{\"project/restore\":{{\"projectPath\":{JsonSerializer.Serialize(path)}}}}}");

        var normalized = EvidenceBuildManifestCompiler.NormalizeProjectAssetsJson(json, RepositoryRoot, PackageRoot);
        using var document = JsonDocument.Parse(normalized);

        Assert.Equal(path,
            document.RootElement.GetProperty("project/restore").GetProperty("projectPath").GetString());
    }

    [Fact]
    public void Raw_bytes_and_normalized_JSON_are_distinct_framed_content_kinds()
    {
        var json = ProjectAssets(RepositoryRoot, PackageRoot, "same", reverseProperties: false);
        var rawDefinition = new EvidenceManifestDefinition([File("a", json)]);
        var normalizedDefinition = new EvidenceManifestDefinition([NormalizedJson("a", json)]);
        var raw = Compile(rawDefinition, [File("a", json)]);
        var normalized = Compile(normalizedDefinition, [NormalizedJson("a", json)]);
        Assert.NotEqual(raw.Sha256, normalized.Sha256);
    }

    [Fact]
    public void Current_pinned_manifest_collections_are_deterministic_read_only_snapshots()
    {
        var first = EvidenceBuildManifest.Create(RepositoryRoot);
        var second = EvidenceBuildManifest.Create(RepositoryRoot);
        Assert.IsType<ReadOnlyCollection<EvidenceBuildInputIdentity>>(first.RequiredInputs);
        Assert.IsType<ReadOnlyCollection<EvidenceBuildPackageIdentity>>(first.RequiredPackages);
        Assert.Equal(first.RequiredInputs, second.RequiredInputs);
        Assert.Equal(first.RequiredPackages, second.RequiredPackages);
        Assert.Equal(EvidenceBuildManifest.GlobalJsonContentSha256, first.GlobalJsonSha256);
    }

    private static EvidenceCompiledManifest Compile(
        EvidenceManifestDefinition definition,
        IEnumerable<EvidenceManifestEntry> observed) =>
        EvidenceBuildManifestCompiler.CompileExact(definition, observed, RepositoryRoot, PackageRoot);

    private static EvidenceManifestEntry File(string path, IEnumerable<byte> content) =>
        new(path, EvidenceManifestRecordKind.File, content);

    private static EvidenceManifestEntry NormalizedJson(string path, IEnumerable<byte> content) =>
        new(path, EvidenceManifestRecordKind.File, content,
            EvidenceManifestContentKind.RepositoryAndPackageRootNormalizedProjectAssetsJson);

    private static EvidenceManifestEntry Directory(string path) =>
        new(path, EvidenceManifestRecordKind.Directory);

    private static byte[] ProjectAssets(
        string repositoryRoot,
        string packageRoot,
        string extraProperty,
        bool reverseProperties)
    {
        var projectPath = repositoryRoot + @"\game\Rounds.Game.csproj";
        var outputPath = repositoryRoot + @"\game\obj";
        var packageWithSlash = packageRoot + @"\";
        var restore = reverseProperties
            ? $"\"outputPath\":{JsonSerializer.Serialize(outputPath)},\"packagesPath\":{JsonSerializer.Serialize(packageWithSlash)},\"projectPath\":{JsonSerializer.Serialize(projectPath)},\"projectUniqueName\":{JsonSerializer.Serialize(projectPath)}"
            : $"\"projectUniqueName\":{JsonSerializer.Serialize(projectPath)},\"projectPath\":{JsonSerializer.Serialize(projectPath)},\"packagesPath\":{JsonSerializer.Serialize(packageWithSlash)},\"outputPath\":{JsonSerializer.Serialize(outputPath)}";
        return Encoding.UTF8.GetBytes(
            $"{{\"version\":3,\"packageFolders\":{{{JsonSerializer.Serialize(packageWithSlash)}:{{}}}},\"project\":{{\"restore\":{{{restore}}}}},\"unrelated\":{JsonSerializer.Serialize(extraProperty)}}}");
    }
}
