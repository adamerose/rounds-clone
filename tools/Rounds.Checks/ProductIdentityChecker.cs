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
            foreach (var failure in CheckRenderedTextExpressions(main))
            {
                failures.Add(failure);
            }
            if (!MaskNonCode(main).Contains("card.DisplayName", StringComparison.Ordinal))
            {
                failures.Add("IDN010 game/Main.cs no longer renders exact sourced draft-card display names.");
            }
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

    private static IReadOnlyList<string> CheckRenderedTextExpressions(string source)
    {
        var failures = new List<string>();
        var masked = MaskNonCode(source);
        var methods = ExtractMethods(source, masked);
        var globalDrawStringCount = CountCallTokens(masked, "DrawString");
        var globalWrapperTokenCount = CountCallTokens(masked, "DrawIncompleteFidelityLine");
        var drawStringCount = 0;
        var wrapperCallCount = 0;
        foreach (var method in methods)
        {
            foreach (var call in ExtractCalls(source, masked, method, "DrawString"))
            {
                drawStringCount++;
                if (call.Arguments.Count <= 2)
                {
                    failures.Add($"IDN010 game/Main.cs has an unrecognized DrawString overload in `{method.Name}`.");
                    continue;
                }
                var renderedText = call.Arguments[2];
                var resolved = ResolveLocals(renderedText, method, call.Start, source, masked);
                if (!IsAllowedDrawStringText(method.Name, renderedText, resolved))
                {
                    failures.Add($"IDN010 game/Main.cs renders an unapproved text expression in `{method.Name}`: `{resolved.Trim()}`.");
                }
            }
            if (method.Name == "DrawIncompleteFidelityLine")
            {
                continue;
            }
            foreach (var call in ExtractCalls(source, masked, method, "DrawIncompleteFidelityLine"))
            {
                wrapperCallCount++;
                if (call.Arguments.Count == 0)
                {
                    failures.Add($"IDN010 game/Main.cs has an unrecognized incomplete-fidelity text call in `{method.Name}`.");
                    continue;
                }
                var resolved = ResolveLocals(call.Arguments[0], method, call.Start, source, masked);
                if (!IncompleteFidelityText().IsMatch(NormalizeExpression(resolved)))
                {
                    failures.Add($"IDN010 game/Main.cs routes unapproved text through DrawIncompleteFidelityLine in `{method.Name}`.");
                }
            }
        }
        if (drawStringCount == 0 ||
            drawStringCount != globalDrawStringCount ||
            wrapperCallCount != 6 ||
            globalWrapperTokenCount != wrapperCallCount + 1)
        {
            failures.Add("IDN010 game/Main.cs text drawing topology changed without updating the fail-closed live-text guard.");
        }
        return failures;
    }

    private static int CountCallTokens(string masked, string callName) =>
        new Regex($@"\b{Regex.Escape(callName)}\s*\(", RegexOptions.CultureInvariant)
            .Matches(masked)
            .Count;

    private static bool IsAllowedDrawStringText(string methodName, string expression, string resolvedExpression)
    {
        var normalized = NormalizeExpression(expression);
        if (methodName == "DrawIncompleteFidelityLine" && normalized == "text")
        {
            return true;
        }
        return AllowedRenderedExpressions.Contains(
            NormalizeExpression(resolvedExpression),
            StringComparer.Ordinal);
    }

    private static readonly string[] AllowedRenderedExpressions =
    [
        NormalizeExpression("""
            _match?.Phase == MatchPhase.MatchResult
                ? _match.WinnerId == 0 ? "RED WINS THE MATCH" : "BLUE WINS THE MATCH"
                : _world.Phase switch
            {
                DuelPhase.Spawning => $"GET READY  {_world.PhaseTicksRemaining}",
                DuelPhase.Resolving => "K.O.",
                DuelPhase.Result when _world.IsDraw => "DRAW",
                DuelPhase.Result => _world.WinnerId == 0 ? "RED WINS" : "BLUE WINS",
                _ => string.Empty,
            }
            """),
        NormalizeExpression("$\"{match.FullPoints[0]}   ROUNDS   {match.FullPoints[1]}\""),
        NormalizeExpression("(_displayCards.GetRequired((match.AcquiredCardsFor(playerId))[(0)])).DisplayName"),
        NormalizeExpression("""
            match.Phase == MatchPhase.OpeningDraft
                ? $"PLAYER {match.CurrentPickerId + 1} — OPENING PICK"
                : $"PLAYER {match.CurrentPickerId + 1} — COMEBACK PICK"
            """),
        NormalizeExpression("match.IsDraftArmed ? \"LEFT / RIGHT TO CHOOSE     JUMP TO TAKE\" : \"RELEASE MOVE AND JUMP\""),
        NormalizeExpression("(match.CurrentOffer[(0)]).DisplayName"),
    ];

    private static string ResolveLocals(
        string expression,
        SourceMethod method,
        int callStart,
        string source,
        string masked)
    {
        var assignments = ExtractAssignments(method, callStart, source, masked);
        return ResolveExpression(expression, assignments, new HashSet<string>(StringComparer.Ordinal));
    }

    private static string ResolveExpression(
        string expression,
        IReadOnlyDictionary<string, string> assignments,
        HashSet<string> resolving)
    {
        return Identifier().Replace(expression, match =>
        {
            var name = match.Value;
            if (!assignments.TryGetValue(name, out var assigned) || !resolving.Add(name))
            {
                return name;
            }
            var resolved = ResolveExpression(assigned, assignments, resolving);
            resolving.Remove(name);
            return $"({resolved})";
        });
    }

    private static IReadOnlyDictionary<string, string> ExtractAssignments(
        SourceMethod method,
        int callStart,
        string source,
        string masked)
    {
        var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefixLength = callStart - method.BodyStart;
        var prefix = masked.Substring(method.BodyStart, prefixLength);
        foreach (Match match in LocalDeclaration().Matches(prefix))
        {
            var equals = method.BodyStart + match.Index + match.Length - 1;
            var end = FindExpressionEnd(masked, equals + 1, callStart);
            if (end > equals)
            {
                assignments[match.Groups[1].Value] = source[(equals + 1)..end];
            }
        }
        foreach (Match match in LocalAssignment().Matches(prefix))
        {
            var name = match.Groups[1].Value;
            var equals = method.BodyStart + match.Index + match.Value.LastIndexOf('=');
            var end = FindExpressionEnd(masked, equals + 1, callStart);
            if (end > equals)
            {
                assignments[name] = source[(equals + 1)..end];
            }
        }
        return assignments;
    }

    private static int FindExpressionEnd(string masked, int start, int limit)
    {
        var round = 0;
        var square = 0;
        var curly = 0;
        for (var index = start; index < limit; index++)
        {
            switch (masked[index])
            {
                case '(': round++; break;
                case ')': round--; break;
                case '[': square++; break;
                case ']': square--; break;
                case '{': curly++; break;
                case '}': curly--; break;
                case ';' when round == 0 && square == 0 && curly == 0: return index;
            }
        }
        return -1;
    }

    private static IReadOnlyList<SourceMethod> ExtractMethods(string source, string masked)
    {
        var methods = new List<SourceMethod>();
        foreach (Match match in MethodDeclaration().Matches(masked))
        {
            var openParenthesis = masked.IndexOf('(', match.Index + match.Length - 1);
            var closeParenthesis = FindBalancedEnd(masked, openParenthesis, '(', ')');
            if (closeParenthesis < 0)
            {
                continue;
            }
            var bodyStart = SkipWhiteSpace(masked, closeParenthesis + 1);
            if (bodyStart < masked.Length && masked[bodyStart] == '{')
            {
                var bodyEnd = FindBalancedEnd(masked, bodyStart, '{', '}');
                if (bodyEnd > bodyStart)
                {
                    methods.Add(new SourceMethod(match.Groups[1].Value, bodyStart + 1, bodyEnd));
                }
            }
            else if (bodyStart + 1 < masked.Length && masked.AsSpan(bodyStart, 2).SequenceEqual("=>"))
            {
                var bodyEnd = masked.IndexOf(';', bodyStart + 2);
                if (bodyEnd > bodyStart)
                {
                    methods.Add(new SourceMethod(match.Groups[1].Value, bodyStart + 2, bodyEnd));
                }
            }
        }
        return methods;
    }

    private static IReadOnlyList<SourceCall> ExtractCalls(
        string source,
        string masked,
        SourceMethod method,
        string callName)
    {
        var calls = new List<SourceCall>();
        var pattern = new Regex($@"\b{Regex.Escape(callName)}\s*\(", RegexOptions.CultureInvariant);
        var body = masked.Substring(method.BodyStart, method.BodyEnd - method.BodyStart);
        foreach (Match match in pattern.Matches(body))
        {
            var start = method.BodyStart + match.Index;
            var open = masked.IndexOf('(', start + callName.Length);
            var close = FindBalancedEnd(masked, open, '(', ')');
            if (close < 0 || close > method.BodyEnd)
            {
                continue;
            }
            calls.Add(new SourceCall(start, SplitArguments(source, masked, open + 1, close)));
        }
        return calls;
    }

    private static IReadOnlyList<string> SplitArguments(string source, string masked, int start, int end)
    {
        var arguments = new List<string>();
        var argumentStart = start;
        var round = 0;
        var square = 0;
        var curly = 0;
        for (var index = start; index < end; index++)
        {
            switch (masked[index])
            {
                case '(': round++; break;
                case ')': round--; break;
                case '[': square++; break;
                case ']': square--; break;
                case '{': curly++; break;
                case '}': curly--; break;
                case ',' when round == 0 && square == 0 && curly == 0:
                    arguments.Add(source[argumentStart..index]);
                    argumentStart = index + 1;
                    break;
            }
        }
        arguments.Add(source[argumentStart..end]);
        return arguments;
    }

    private static int FindBalancedEnd(string masked, int start, char open, char close)
    {
        if (start < 0 || start >= masked.Length || masked[start] != open)
        {
            return -1;
        }
        var depth = 0;
        for (var index = start; index < masked.Length; index++)
        {
            if (masked[index] == open) depth++;
            else if (masked[index] == close && --depth == 0) return index;
        }
        return -1;
    }

    private static int SkipWhiteSpace(string text, int start)
    {
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        return start;
    }

    private static string NormalizeExpression(string expression) =>
        WhiteSpace().Replace(expression.Trim().Trim('(', ')'), string.Empty);

    private static string MaskComments(string source) => MaskSource(source, maskStrings: false);

    private static string MaskNonCode(string source) => MaskSource(source, maskStrings: true);

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

    private sealed record SourceMethod(string Name, int BodyStart, int BodyEnd);
    private sealed record SourceCall(int Start, IReadOnlyList<string> Arguments);

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

    [GeneratedRegex(@"(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|sealed|async|readonly|partial)\s+)*[A-Za-z_][\w<>,.?\[\]]*\s+([A-Za-z_]\w*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MethodDeclaration();

    [GeneratedRegex(@"\b(?:var|string)\s+([A-Za-z_]\w*)\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex LocalDeclaration();

    [GeneratedRegex(@"(?m)(?:^|[;{}])\s*([A-Za-z_]\w*)\s*=(?!=)", RegexOptions.CultureInvariant)]
    private static partial Regex LocalAssignment();

    [GeneratedRegex(@"\b[A-Za-z_]\w*\b", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^FaithfulSubsetMatchShell\.IncompleteFidelity(?:Headline|Subtitle)Line[123]$", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteFidelityText();
}
