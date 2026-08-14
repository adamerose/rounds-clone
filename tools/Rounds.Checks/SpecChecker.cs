using System.Text.Json;

namespace Rounds.Checks;

public static class SpecChecker
{
    private static readonly string[] RequiredMeasuredFacts =
    [
        "player-diameter",
        "player-run-speed",
        "player-jump-apex-height",
        "player-jump-apex-time",
        "combat-projectile-speed",
        "combat-projectile-radius",
        "combat-recoil-speed",
        "combat-block-window",
        "camera-horizontal-span",
        "camera-out-of-bounds-result-delay",
    ];

    private static readonly IReadOnlyDictionary<string, string> RequiredDocuments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sources.json"] = "source-index.schema.json",
        ["cards.json"] = "cards.schema.json",
        ["match.json"] = "mechanics.schema.json",
        ["controls.json"] = "mechanics.schema.json",
        ["player.json"] = "mechanics.schema.json",
        ["combat.json"] = "mechanics.schema.json",
        ["camera.json"] = "mechanics.schema.json",
        ["measurements.json"] = "measurements.schema.json",
    };

    private static readonly HashSet<string> KnownCardTargets =
    [
        "player.block-cooldown",
        "player.lifesteal",
        "player.max-health",
        "player.move-speed",
        "weapon.ammo",
        "weapon.attack-speed",
        "weapon.damage",
        "weapon.projectile-bounces",
        "weapon.projectile-size",
        "weapon.projectile-slow",
        "weapon.projectile-speed",
        "weapon.projectiles-per-shot",
        "weapon.reload-speed",
        "weapon.reload-time",
        "weapon.splash-damage",
        "weapon.wall-drill-distance",
        "ability.abyssal-countdown",
        "ability.bombs-away",
        "ability.brawler",
        "ability.brawler-duration",
        "ability.burst",
        "ability.chase",
        "ability.chilling-presence",
        "ability.cold-bullets",
        "ability.damage-decay",
        "ability.damage-decay-duration",
        "ability.dazzle",
        "ability.demonic-pact",
        "ability.demonic-pact-cost",
        "ability.drill-ammo",
        "ability.echo",
        "ability.emp",
        "ability.empower",
        "ability.explosive-bullet",
        "ability.frost-slam",
        "ability.grow",
        "ability.grow-distance",
        "ability.grow-maximum-damage",
        "ability.healing-field",
        "ability.healing-field-activation",
        "ability.homing",
        "ability.implode",
        "ability.lifestealer",
        "ability.lifestealer-drain",
        "ability.overpower",
        "ability.overpower-damage",
        "ability.parasite",
        "ability.parasite-duration",
        "ability.phoenix",
        "ability.poison",
        "ability.poison-duration",
        "ability.pristine-perseverence",
        "ability.pristine-perseverence-threshold",
        "ability.radar-shot",
        "ability.radiance",
        "ability.refresh",
        "ability.remote",
        "ability.riccochet",
        "ability.saw",
        "ability.scavenger",
        "ability.shield-charge",
        "ability.shields-up",
        "ability.shockwave",
        "ability.silence",
        "ability.sneaky",
        "ability.static-field",
        "ability.supernova",
        "ability.tactical-reload",
        "ability.target-bounce",
        "ability.taste-of-blood",
        "ability.taste-of-blood-duration",
        "ability.teleport",
        "ability.thruster",
        "ability.timed-detonation",
        "ability.timed-detonation-delay",
        "ability.toxic-cloud",
        "ability.trickster",
        "ability.trickster-damage-per-bounce",
    ];

    public static IReadOnlyList<string> CheckRepository(string repository)
    {
        var failures = new List<string>();
        var specRoot = Path.Combine(repository, "spec");
        var schemaRoot = Path.Combine(specRoot, "schema");
        var documents = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        var schemas = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

        try
        {
            foreach (var schemaName in RequiredDocuments.Values.Distinct(StringComparer.Ordinal))
            {
                schemas[schemaName] = Parse(Path.Combine(schemaRoot, schemaName), failures);
            }

            foreach (var (documentName, schemaName) in RequiredDocuments)
            {
                var document = Parse(Path.Combine(specRoot, documentName), failures);
                documents[documentName] = document;
                if (document.RootElement.ValueKind == JsonValueKind.Null || schemas[schemaName].RootElement.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                foreach (var failure in JsonSchemaSubsetValidator.Validate(schemas[schemaName].RootElement, document.RootElement))
                {
                    failures.Add($"SPEC001 {documentName} {failure}");
                }
            }

            if (failures.Any(failure => failure.StartsWith("SPEC000", StringComparison.Ordinal) || failure.StartsWith("SPEC001", StringComparison.Ordinal)))
            {
                return failures;
            }

            CrossCheck(documents, failures);
            return failures;
        }
        finally
        {
            foreach (var document in documents.Values)
            {
                document.Dispose();
            }

            foreach (var schema in schemas.Values)
            {
                schema.Dispose();
            }
        }
    }

    private static JsonDocument Parse(string path, List<string> failures)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            failures.Add($"SPEC000 {Path.GetFileName(path)} could not be read as JSON: {exception.Message}");
            return JsonDocument.Parse("null");
        }
    }

    private static void CrossCheck(IReadOnlyDictionary<string, JsonDocument> documents, List<string> failures)
    {
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in documents["sources.json"].RootElement.GetProperty("sources").EnumerateArray())
        {
            var sourceId = source.GetProperty("id").GetString()!;
            if (!sourceIds.Add(sourceId))
            {
                failures.Add($"SPEC002 sources.json duplicates source id `{sourceId}`.");
            }
        }

        CrossCheckCards(documents["cards.json"].RootElement, sourceIds, failures);

        var factIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var documentName in RequiredDocuments.Keys.Where(name => RequiredDocuments[name] == "mechanics.schema.json"))
        {
            var root = documents[documentName].RootElement;
            var expectedKind = Path.GetFileNameWithoutExtension(documentName);
            if (root.GetProperty("kind").GetString() != expectedKind)
            {
                failures.Add($"SPEC003 {documentName} kind must be `{expectedKind}`.");
            }

            foreach (var fact in root.GetProperty("facts").EnumerateArray())
            {
                var factId = fact.GetProperty("id").GetString()!;
                if (!factIds.Add(factId))
                {
                    failures.Add($"SPEC004 {documentName} duplicates fact id `{factId}`.");
                }

                foreach (var source in fact.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!))
                {
                    if (!sourceIds.Contains(source))
                    {
                        failures.Add($"SPEC005 {documentName} fact `{factId}` cites unknown source `{source}`.");
                    }
                }
            }
        }

        var measurementRoot = documents["measurements.json"].RootElement;
        var measurementIds = new HashSet<string>(StringComparer.Ordinal);
        var measuredSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var measurement in measurementRoot.GetProperty("measurements").EnumerateArray())
        {
            var measurementId = measurement.GetProperty("id").GetString()!;
            var factId = measurement.GetProperty("metricFactId").GetString()!;
            var sourceId = measurement.GetProperty("source").GetString()!;
            if (!measurementIds.Add(measurementId))
            {
                failures.Add($"SPEC008 measurements.json duplicates measurement id `{measurementId}`.");
            }

            if (!factIds.Contains(factId))
            {
                failures.Add($"SPEC006 measurements.json measurement `{measurementId}` targets unknown fact `{factId}`.");
            }

            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC007 measurements.json measurement `{measurementId}` cites unknown source `{sourceId}`.");
            }

            if (measurement.GetProperty("countsTowardCoverage").GetBoolean())
            {
                if (!measuredSources.TryGetValue(factId, out var sources))
                {
                    sources = new HashSet<string>(StringComparer.Ordinal);
                    measuredSources[factId] = sources;
                }

                sources.Add(sourceId);
            }

            var pixelMeasurements = measurement.GetProperty("pixelMeasurements");
            ValidateRecordedSpan(pixelMeasurements, "startCenterX", "endCenterX", "distancePixels", measurementId, failures);
            ValidateRecordedSpan(pixelMeasurements, "startCenterY", "apexCenterY", "verticalRisePixels", measurementId, failures);

            var derivation = measurement.GetProperty("derivation");
            var operation = derivation.GetProperty("operation").GetString()!;
            var operands = new List<double>();
            foreach (var operandElement in derivation.GetProperty("operands").EnumerateArray())
            {
                var operand = operandElement.GetString()!;
                if (operand == "observedFrameIntervalTicks")
                {
                    operands.Add(measurement.GetProperty(operand).GetDouble());
                    continue;
                }

                var field = operand["pixelMeasurements.".Length..];
                if (measurement.GetProperty("pixelMeasurements").TryGetProperty(field, out var value))
                {
                    operands.Add(value.GetDouble());
                    continue;
                }

                failures.Add($"SPEC016 measurements.json measurement `{measurementId}` derivation cites missing raw field `{operand}`.");
            }

            if (operands.Count != derivation.GetProperty("operands").GetArrayLength())
            {
                continue;
            }

            var recomputed = operation switch
            {
                "identity" => operands[0],
                "divide" => operands.Skip(1).Aggregate(operands[0], (result, operand) => result / operand),
                "multiply" => operands.Aggregate(1.0, (result, operand) => result * operand),
                _ => double.NaN,
            };
            var recorded = measurement.GetProperty("normalizedValue").GetDouble();
            if (!double.IsFinite(recomputed) || Math.Abs(recomputed - recorded) > 0.0001)
            {
                failures.Add($"SPEC009 measurements.json measurement `{measurementId}` records {recorded} but its derivation recomputes to {recomputed:F6}.");
            }
        }

        var coveredFacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coverage in measurementRoot.GetProperty("coverage").EnumerateArray())
        {
            var factId = coverage.GetProperty("metricFactId").GetString()!;
            if (!coveredFacts.Add(factId))
            {
                failures.Add($"SPEC010 measurements.json duplicates coverage for fact `{factId}`.");
            }

            if (!factIds.Contains(factId))
            {
                failures.Add($"SPEC011 measurements.json covers unknown fact `{factId}`.");
            }

            var minimum = coverage.GetProperty("minimumIndependentSources").GetInt32();
            var actual = measuredSources.TryGetValue(factId, out var sources) ? sources.Count : 0;
            if (actual < minimum)
            {
                failures.Add($"SPEC012 measurements.json fact `{factId}` requires {minimum} independent sources but has {actual}.");
            }

            if (minimum < 2 && !coverage.TryGetProperty("limitation", out _))
            {
                failures.Add($"SPEC013 measurements.json fact `{factId}` allows fewer than two sources without documenting the limitation.");
            }
        }

        foreach (var factId in measuredSources.Keys)
        {
            if (!coveredFacts.Contains(factId))
            {
                failures.Add($"SPEC014 measurements.json fact `{factId}` has accepted measurements but no coverage contract.");
            }
        }

        if (RequiredMeasuredFacts.Any(factIds.Contains))
        {
            foreach (var factId in RequiredMeasuredFacts.Where(factId => !coveredFacts.Contains(factId)))
            {
                failures.Add($"SPEC015 measurements.json omits required coverage for fact `{factId}`.");
            }
        }
    }

    private static void CrossCheckCards(JsonElement root, HashSet<string> sourceIds, List<string> failures)
    {
        var sourceExclusions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var exclusion in root.GetProperty("sourceExclusions").EnumerateArray())
        {
            var source = exclusion.GetProperty("source").GetString()!;
            var cardId = exclusion.GetProperty("cardId").GetString()!;
            var fact = exclusion.GetProperty("fact").GetString()!;
            var key = $"{source}/{cardId}/{fact}";
            if (!sourceExclusions.TryAdd(key, exclusion.GetProperty("reason").GetString()!))
            {
                failures.Add($"SPEC034 cards.json duplicates source exclusion `{key}`.");
            }

            if (!sourceIds.Contains(source))
            {
                failures.Add($"SPEC035 cards.json source exclusion `{key}` cites unknown source `{source}`.");
            }
        }

        var evaluationOrders = new HashSet<int>();
        foreach (var phase in root.GetProperty("evaluationOrder").EnumerateArray())
        {
            var order = phase.GetProperty("order").GetInt32();
            if (!evaluationOrders.Add(order))
            {
                failures.Add($"SPEC028 cards.json duplicates evaluation order `{order}`.");
            }
        }

        var cards = root.GetProperty("cards").EnumerateArray().ToArray();
        if (cards.Length != root.GetProperty("catalogCount").GetInt32())
        {
            failures.Add($"SPEC018 cards.json declares {root.GetProperty("catalogCount").GetInt32()} cards but contains {cards.Length}.");
        }

        var cardEffects = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var cardEffectStacking = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            var cardId = card.GetProperty("id").GetString()!;
            if (!cardEffects.TryAdd(cardId, new HashSet<string>(StringComparer.Ordinal)))
            {
                failures.Add($"SPEC019 cards.json duplicates card id `{cardId}`.");
                continue;
            }

            ValidateProvenanceSources(card.GetProperty("metadataProvenance"), sourceIds, $"card `{cardId}` metadata", failures);
            ValidateProvenanceSources(card.GetProperty("behavior").GetProperty("provenance"), sourceIds, $"card `{cardId}` behavior", failures);
            ValidateExcludedSources(card.GetProperty("metadataProvenance"), sourceExclusions, cardId, "metadata", failures);
            ValidateExcludedSources(card.GetProperty("behavior").GetProperty("provenance"), sourceExclusions, cardId, "behavior", failures);

            foreach (var effect in card.GetProperty("effects").EnumerateArray())
            {
                var effectId = effect.GetProperty("id").GetString()!;
                if (!cardEffects[cardId].Add(effectId))
                {
                    failures.Add($"SPEC020 cards.json card `{cardId}` duplicates effect id `{effectId}`.");
                }

                cardEffectStacking[$"{cardId}/{effectId}"] = effect.GetProperty("stacking").GetString()!;

                var target = effect.GetProperty("target").GetString()!;
                if (!KnownCardTargets.Contains(target))
                {
                    failures.Add($"SPEC021 cards.json card `{cardId}` effect `{effectId}` targets unknown stat or hook `{target}`.");
                }

                var provenance = effect.GetProperty("provenance");
                ValidateProvenanceSources(provenance, sourceIds, $"card `{cardId}` effect `{effectId}`", failures);
                var stackingProvenance = effect.GetProperty("stackingProvenance");
                ValidateProvenanceSources(stackingProvenance, sourceIds, $"card `{cardId}` effect `{effectId}` stacking and cap", failures);
                ValidateExcludedSources(provenance, sourceExclusions, cardId, $"effect:{effectId}", failures);
                ValidateExcludedSources(stackingProvenance, sourceExclusions, cardId, $"effect:{effectId}", failures);

                var order = effect.GetProperty("order").GetInt32();
                if (!evaluationOrders.Contains(order))
                {
                    failures.Add($"SPEC029 cards.json card `{cardId}` effect `{effectId}` uses undefined evaluation order `{order}`.");
                }

                var operation = effect.GetProperty("operation").GetString()!;
                var unit = effect.GetProperty("unit").GetString()!;
                var requiredUnit = operation switch
                {
                    "multiply" => "factor",
                    "add-percent" => "percent",
                    "add-count" => "count",
                    "register-hook" => "presence",
                    _ => null,
                };
                if ((requiredUnit is not null && unit != requiredUnit) || (unit == "factor" && operation != "multiply"))
                {
                    failures.Add($"SPEC030 cards.json card `{cardId}` effect `{effectId}` uses operation `{operation}` with incompatible unit `{unit}`.");
                }

                var stacking = effect.GetProperty("stacking").GetString()!;
                var cap = effect.GetProperty("cap").GetString()!;
                var stackingStatus = stackingProvenance.GetProperty("status").GetString()!;
                if ((stacking != "unresolved" || cap != "unknown") && stackingStatus == "unknown")
                {
                    failures.Add($"SPEC031 cards.json card `{cardId}` effect `{effectId}` asserts stacking or a cap without supporting provenance.");
                }

                if (effect.GetProperty("operation").GetString() != "register-hook")
                {
                    var effectSources = provenance.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!).ToArray();
                    if (effectSources.Length < 2 && !effectSources.Contains("patch-105", StringComparer.Ordinal))
                    {
                        failures.Add($"SPEC022 cards.json card `{cardId}` numeric effect `{effectId}` lacks an official note, direct observation, or two corroborating sources.");
                    }
                }
            }
        }

        foreach (var exclusion in root.GetProperty("sourceExclusions").EnumerateArray())
        {
            var cardId = exclusion.GetProperty("cardId").GetString()!;
            var fact = exclusion.GetProperty("fact").GetString()!;
            if (!cardEffects.TryGetValue(cardId, out var effectIds) ||
                (fact.StartsWith("effect:", StringComparison.Ordinal) && fact != "effect:*" && !effectIds.Contains(fact["effect:".Length..])))
            {
                failures.Add($"SPEC036 cards.json source exclusion targets missing fact `{cardId}/{fact}`.");
            }
        }

        foreach (var reconciliation in root.GetProperty("reconciliation").EnumerateArray())
        {
            var sourceId = reconciliation.GetProperty("source").GetString()!;
            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC023 cards.json reconciliation cites unknown source `{sourceId}`.");
            }
        }

        foreach (var patch in root.GetProperty("patchHistory").EnumerateArray())
        {
            var sourceId = patch.GetProperty("source").GetString()!;
            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC027 cards.json patch history cites unknown source `{sourceId}`.");
            }
        }

        var stackingOperators = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stackingCase in root.GetProperty("stackingCases").EnumerateArray())
        {
            var stackingOperator = stackingCase.GetProperty("operator").GetString()!;
            stackingOperators.Add(stackingOperator);
            var cardId = stackingCase.GetProperty("cardId").GetString()!;
            var effectId = stackingCase.GetProperty("effectId").GetString()!;
            var effectKey = $"{cardId}/{effectId}";
            if (!cardEffects.TryGetValue(cardId, out var effectIds) || !effectIds.Contains(effectId))
            {
                failures.Add($"SPEC024 cards.json stacking case `{stackingOperator}` targets missing effect `{cardId}/{effectId}`.");
            }

            else
            {
                var resolution = stackingCase.GetProperty("resolution").GetString()!;
                var effectStacking = cardEffectStacking[effectKey];
                var expectedStacking = stackingOperator == "hook" ? "hook-per-copy" : stackingOperator;
                if (resolution == "unresolved" && effectStacking != "unresolved")
                {
                    failures.Add($"SPEC032 cards.json unresolved stacking case `{stackingOperator}` asserts `{effectStacking}` on `{effectKey}`.");
                }
                else if (resolution != "unresolved" && effectStacking != expectedStacking)
                {
                    failures.Add($"SPEC033 cards.json resolved stacking case `{stackingOperator}` disagrees with `{effectKey}` operator `{effectStacking}`.");
                }
            }

            ValidateProvenanceSources(stackingCase.GetProperty("provenance"), sourceIds, $"stacking case `{stackingOperator}`", failures);
        }

        foreach (var requiredOperator in new[] { "additive", "multiplicative", "count", "max-wins", "hook" })
        {
            if (!stackingOperators.Contains(requiredOperator))
            {
                failures.Add($"SPEC025 cards.json omits representative `{requiredOperator}` stacking semantics.");
            }
        }
    }

    private static void ValidateProvenanceSources(
        JsonElement provenance,
        HashSet<string> sourceIds,
        string subject,
        List<string> failures)
    {
        foreach (var sourceId in provenance.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!))
        {
            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC026 cards.json {subject} cites unknown source `{sourceId}`.");
            }
        }
    }

    private static void ValidateExcludedSources(
        JsonElement provenance,
        IReadOnlyDictionary<string, string> sourceExclusions,
        string cardId,
        string fact,
        List<string> failures)
    {
        foreach (var sourceId in provenance.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!))
        {
            var exactKey = $"{sourceId}/{cardId}/{fact}";
            var wildcardKey = $"{sourceId}/{cardId}/effect:*";
            if (sourceExclusions.TryGetValue(exactKey, out var reason) ||
                (fact.StartsWith("effect:", StringComparison.Ordinal) && sourceExclusions.TryGetValue(wildcardKey, out reason)))
            {
                failures.Add($"SPEC037 cards.json card `{cardId}` fact `{fact}` cites excluded source `{sourceId}`: {reason}");
            }
        }
    }

    private static void ValidateRecordedSpan(
        JsonElement pixelMeasurements,
        string startField,
        string endField,
        string spanField,
        string measurementId,
        List<string> failures)
    {
        if (!pixelMeasurements.TryGetProperty(startField, out var start) ||
            !pixelMeasurements.TryGetProperty(endField, out var end) ||
            !pixelMeasurements.TryGetProperty(spanField, out var span))
        {
            return;
        }

        var recomputed = Math.Abs(end.GetDouble() - start.GetDouble());
        var recorded = span.GetDouble();
        if (Math.Abs(recomputed - recorded) > 0.0001)
        {
            failures.Add($"SPEC017 measurements.json measurement `{measurementId}` records {spanField} as {recorded} but its endpoints recompute to {recomputed:F6}.");
        }
    }
}
