using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rounds.Checks;

public static partial class ProductIdentityChecker
{
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
        var main = ReadRequired(repository, Path.Combine("game", "Main.cs"), failures);
        var definition = ReadRequired(
            repository,
            Path.Combine("src", "Rounds.Sim", "Cards", "StatCardDefinition.cs"),
            failures);
        var catalog = ReadRequired(
            repository,
            Path.Combine("src", "Rounds.Sim", "Cards", "StatCardCatalog.cs"),
            failures);
        if (main is not null)
        {
            var forbiddenMainTokens = new[]
            {
                ".Arena.Id",
                ".BouncesRemaining",
                ".Bullets.Count",
                ".DuelNumber",
                "AimDirection.X",
                "AimDirection.Y",
                "_world.Phase.ToString",
                "_match.Phase.ToString",
                "BlockTicksRemaining",
                "BLOCK READY",
                "blockText",
                "card.Summary",
                "card.Effects",
                "EffectLine(",
            };
            foreach (var token in forbiddenMainTokens)
            {
                if (main.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"IDN010 game/Main.cs exposes unsupported live UI through `{token}`.");
                }
            }
            if (RenderedPhaseInterpolation().IsMatch(main))
            {
                failures.Add("IDN010 game/Main.cs directly renders an internal duel or match phase value.");
            }
            if (!main.Contains("card.DisplayName", StringComparison.Ordinal))
            {
                failures.Add("IDN010 game/Main.cs no longer renders exact sourced draft-card display names.");
            }
        }
        if (definition is not null && SummaryIdentifier().IsMatch(definition))
        {
            failures.Add("IDN011 StatCardDefinition.cs reintroduces a runtime card summary surface.");
        }
        if (catalog is not null && SummaryIdentifier().IsMatch(catalog))
        {
            failures.Add("IDN012 StatCardCatalog.cs reintroduces a hard-coded runtime card summary catalog.");
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

    [GeneratedRegex(@"\{\s*_?(?:world|match)\.Phase(?:\s*[,}:])", RegexOptions.CultureInvariant)]
    private static partial Regex RenderedPhaseInterpolation();

    [GeneratedRegex(@"\bSummary(?:For)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex SummaryIdentifier();
}
