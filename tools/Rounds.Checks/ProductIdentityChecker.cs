using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rounds.Checks;

public static partial class ProductIdentityChecker
{
    private const string ExpectedShippedRuntimeBoundarySha256 =
        "795aa2aaf2938491f2c6f4b69936076a1592a6352ade2a4f13df706eb19793cb";

    private static readonly string[] ShippedRuntimeRoots =
    [
        "game",
        "spec",
        "src/Rounds.Sim",
        "src/Rounds.Replay",
    ];

    private static readonly HashSet<string> GeneratedRuntimeDirectoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".godot",
            "bin",
            "obj",
        };

    private static readonly HashSet<string> RepositoryTraversalExclusions =
        new(GeneratedRuntimeDirectoryNames, StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".ivy",
            ".tools",
        };

    private static readonly HashSet<string> BuildControlExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".props",
            ".targets",
            ".rsp",
        };

    private static readonly HashSet<string> AutomaticBuildControlNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "global.json",
            "NuGet.Config",
        };

    private static readonly HashSet<string> RootSourceExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".fs",
            ".vb",
            ".csx",
        };

    private static readonly string[] ActiveIdentityPaths =
    [
        "GOAL.md",
        "README.md",
        "game/project.godot",
        "game/Main.cs",
        "docs/architecture.md",
        "docs/design/visual-system.md",
        "research/notes/core-rules.md",
    ];

    private static readonly IReadOnlyDictionary<string, (string Id, string Title)> Schemas =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["cards.schema.json"] = ("https://ricochet.local/schema/cards.schema.json", "ROUNDS clean-room vanilla card catalog"),
            ["maps.schema.json"] = ("https://ricochet.local/schema/maps.schema.json", "ROUNDS clean-room vanilla arena catalog"),
            ["measurements.schema.json"] = ("https://ricochet.local/schema/measurements.schema.json", "ROUNDS frame measurement log"),
            ["mechanics.schema.json"] = ("https://ricochet.local/schema/mechanics.schema.json", "ROUNDS sourced mechanics specification"),
            ["source-index.schema.json"] = ("https://ricochet.local/schema/source-index.schema.json", "ROUNDS fidelity source index"),
        };

    public static IReadOnlyList<string> CheckRepository(string repository)
    {
        var failures = new List<string>();
        foreach (var relativePath in ActiveIdentityPaths)
        {
            var text = ReadRequired(repository, relativePath, failures);
            if (text is null)
            {
                continue;
            }

            if (!text.Contains("ROUNDS", StringComparison.Ordinal))
            {
                failures.Add($"IDN001 {relativePath} does not identify the faithful target as `ROUNDS`.");
            }
            if (text.Contains("RICOCHET", StringComparison.Ordinal))
            {
                failures.Add($"IDN002 {relativePath} retains the superseded `RICOCHET` product title.");
            }
        }

        CheckSchemas(repository, failures);
        CheckSupportedCardNames(repository, failures);
        CheckUnsupportedLiveUiIsAbsent(repository, failures);
        return failures;
    }

    private static void CheckSchemas(string repository, List<string> failures)
    {
        foreach (var (filename, expected) in Schemas)
        {
            var relativePath = Path.Combine("spec", "schema", filename);
            var text = ReadRequired(repository, relativePath, failures);
            if (text is null)
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.GetProperty("$id").GetString() != expected.Id)
                {
                    failures.Add($"IDN003 {relativePath} changed its stable `$id`.");
                }
                if (root.GetProperty("title").GetString() != expected.Title)
                {
                    failures.Add($"IDN004 {relativePath} must use the exact `ROUNDS` metadata title.");
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                failures.Add($"IDN005 {relativePath} cannot be read as identity metadata: {exception.Message}");
            }
        }
    }

    private static void CheckSupportedCardNames(string repository, List<string> failures)
    {
        var cardsText = ReadRequired(repository, Path.Combine("spec", "cards.json"), failures);
        var catalogText = ReadRequired(repository, Path.Combine("src", "Rounds.Sim", "Cards", "StatCardCatalog.cs"), failures);
        if (cardsText is null || catalogText is null)
        {
            return;
        }

        Dictionary<string, string> sourcedNames;
        try
        {
            using var document = JsonDocument.Parse(cardsText);
            sourcedNames = document.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(
                card => card.GetProperty("id").GetString()!,
                card => card.GetProperty("originalName").GetString()!,
                StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            failures.Add($"IDN006 spec/cards.json cannot supply sourced card names: {exception.Message}");
            return;
        }

        var supportedBlock = SupportedIdsBlock().Match(catalogText);
        var namesBlock = OriginalNamesBlock().Match(catalogText);
        if (!supportedBlock.Success || !namesBlock.Success)
        {
            failures.Add("IDN007 StatCardCatalog.cs does not expose the supported IDs and sourced-name guard in the checked form.");
            return;
        }

        var supportedIds = QuotedToken().Matches(supportedBlock.Groups[1].Value)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        var runtimeNames = OriginalNamePair().Matches(namesBlock.Groups[1].Value)
            .ToDictionary(
                match => match.Groups[1].Value,
                match => match.Groups[2].Value,
                StringComparer.Ordinal);
        if (supportedIds.Length != 16 || supportedIds.Distinct(StringComparer.Ordinal).Count() != 16)
        {
            failures.Add("IDN008 the shipped draft pool must contain exactly 16 distinct sourced IDs.");
            return;
        }

        foreach (var id in supportedIds)
        {
            if (!sourcedNames.TryGetValue(id, out var sourcedName) ||
                !runtimeNames.TryGetValue(id, out var runtimeName) ||
                runtimeName != sourcedName)
            {
                failures.Add($"IDN009 supported card `{id}` does not map to its exact spec originalName.");
            }
        }
    }

    private static void CheckUnsupportedLiveUiIsAbsent(string repository, List<string> failures)
    {
        var definition = ReadRequired(
            repository,
            Path.Combine("src", "Rounds.Sim", "Cards", "StatCardDefinition.cs"),
            failures);
        var catalog = ReadRequired(
            repository,
            Path.Combine("src", "Rounds.Sim", "Cards", "StatCardCatalog.cs"),
            failures);
        var actualBoundarySha256 = ComputeShippedRuntimeBoundarySha256(repository);
        if (actualBoundarySha256 != ExpectedShippedRuntimeBoundarySha256)
        {
            failures.Add(
                "IDN010 the complete shipped runtime/build boundary changed; " +
                "deliberately review every included input and update ExpectedShippedRuntimeBoundarySha256.");
        }
        if (definition is not null && SummaryIdentifier().IsMatch(MaskComments(definition)))
        {
            failures.Add("IDN011 StatCardDefinition.cs reintroduces a runtime card summary surface.");
        }
        if (catalog is not null && SummaryIdentifier().IsMatch(MaskComments(catalog)))
        {
            failures.Add("IDN012 StatCardCatalog.cs reintroduces a hard-coded runtime card summary catalog.");
        }
    }

    private static string ComputeShippedRuntimeBoundarySha256(string repository)
    {
        var relativePaths = EnumerateShippedRuntimeBoundaryFiles(repository);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var contentBuffer = new byte[81920];
        foreach (var relativePath in relativePaths)
        {
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            AppendInt64(hash, pathBytes.LongLength);
            hash.AppendData(pathBytes);

            using var content = File.OpenRead(Path.Combine(
                repository,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            AppendInt64(hash, content.Length);
            int bytesRead;
            while ((bytesRead = content.Read(contentBuffer, 0, contentBuffer.Length)) != 0)
            {
                hash.AppendData(contentBuffer, 0, bytesRead);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IReadOnlyList<string> EnumerateShippedRuntimeBoundaryFiles(string repository)
    {
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runtimeRootRelativePath in ShippedRuntimeRoots)
        {
            var runtimeRoot = Path.Combine(
                repository,
                runtimeRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(runtimeRoot))
            {
                continue;
            }

            AddFilesRecursively(
                repository,
                runtimeRoot,
                GeneratedRuntimeDirectoryNames,
                _ => true,
                relativePaths);
        }

        AddFilesRecursively(
            repository,
            repository,
            RepositoryTraversalExclusions,
            IsRepositoryBuildControl,
            relativePaths);

        foreach (var file in Directory.EnumerateFiles(repository, "*", SearchOption.TopDirectoryOnly))
        {
            if (RootSourceExtensions.Contains(Path.GetExtension(file)))
            {
                relativePaths.Add(ToCanonicalRelativePath(repository, file));
            }
        }

        return relativePaths.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddFilesRecursively(
        string repository,
        string root,
        IReadOnlySet<string> excludedDirectoryNames,
        Func<string, bool> includeFile,
        ISet<string> relativePaths)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                if (!excludedDirectoryNames.Contains(Path.GetFileName(childDirectory)))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (includeFile(file))
                {
                    relativePaths.Add(ToCanonicalRelativePath(repository, file));
                }
            }
        }
    }

    private static bool IsRepositoryBuildControl(string path) =>
        BuildControlExtensions.Contains(Path.GetExtension(path)) ||
        AutomaticBuildControlNames.Contains(Path.GetFileName(path));

    private static string ToCanonicalRelativePath(string repository, string path) =>
        Path.GetRelativePath(repository, path).Replace(Path.DirectorySeparatorChar, '/');

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, value);
        hash.AppendData(length);
    }

    private static string MaskComments(string source) => MaskSource(source, maskStrings: false);

    private static string MaskSource(string source, bool maskStrings)
    {
        var result = source.ToCharArray();
        for (var index = 0; index < source.Length;)
        {
            if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '/')
            {
                var end = source.IndexOf('\n', index + 2);
                if (end < 0) end = source.Length;
                Mask(result, index, end);
                index = end;
            }
            else if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '*')
            {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Mask(result, index, end);
                index = end;
            }
            else if (source[index] is '"' or '\'')
            {
                var quote = source[index];
                var verbatim = quote == '"' && index > 0 && source[index - 1] == '@';
                var end = index + 1;
                while (end < source.Length)
                {
                    if (!verbatim && source[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }
                    if (source[end] == quote)
                    {
                        if (verbatim && end + 1 < source.Length && source[end + 1] == '"')
                        {
                            end += 2;
                            continue;
                        }
                        end++;
                        break;
                    }
                    end++;
                }
                if (maskStrings) Mask(result, index, end);
                index = end;
            }
            else
            {
                index++;
            }
        }
        return new string(result);
    }

    private static void Mask(char[] text, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (text[index] is not ('\r' or '\n')) text[index] = ' ';
        }
    }

    private static string? ReadRequired(string repository, string relativePath, List<string> failures)
    {
        var path = Path.Combine(repository, relativePath);
        if (!File.Exists(path))
        {
            failures.Add($"IDN000 required identity surface `{relativePath}` is missing.");
            return null;
        }
        return File.ReadAllText(path);
    }

    [GeneratedRegex(@"SupportedIds\s*=\s*\[(.*?)\];", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SupportedIdsBlock();

    [GeneratedRegex(@"private static void ValidateOriginalName.*?var expected = id switch\s*\{(.*?)\n\s*\};", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex OriginalNamesBlock();

    [GeneratedRegex("\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedToken();

    [GeneratedRegex("\"([^\"]+)\"\\s*=>\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex OriginalNamePair();

    [GeneratedRegex(@"\bSummary(?:For)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex SummaryIdentifier();

}
