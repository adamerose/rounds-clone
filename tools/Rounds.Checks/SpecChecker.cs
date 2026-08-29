using System.Security.Cryptography;
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

    private const string ProjectileFrameSpanType = "projectile-frame-span";
    private const string ProjectileSourceSha256 = "D6383E0C7A10EC4CE89C4E551FA7D1817A5D0D120BF17B025FA553442973C36C";

    private static readonly IReadOnlyDictionary<string, (int FirstFrame, int LastFrame, string FirstTimestamp, string LastTimestamp)> RequiredProjectileFrameSpans =
        new Dictionary<string, (int FirstFrame, int LastFrame, string FirstTimestamp, string LastTimestamp)>(StringComparer.Ordinal)
        {
            ["m-projectile-speed-ssag-track-a"] = (838, 844, "00:00:27.933", "00:00:28.133"),
            ["m-projectile-speed-ssag-track-b"] = (972, 981, "00:00:32.400", "00:00:32.700"),
        };

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
        ["maps.json"] = "maps.schema.json",
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
        var projectileFrameSpanIds = new HashSet<string>(StringComparer.Ordinal);
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
            if (measurement.TryGetProperty("measurementType", out var measurementType) &&
                measurementType.GetString() == ProjectileFrameSpanType)
            {
                projectileFrameSpanIds.Add(measurementId);
                ValidateProjectileFrameSpan(measurement, measurementId, failures);
            }

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

        if (factIds.Contains("combat-projectile-speed"))
        {
            foreach (var requiredId in RequiredProjectileFrameSpans.Keys.Where(id => !projectileFrameSpanIds.Contains(id)))
            {
                failures.Add($"SPEC061 measurements.json omits required projectile frame-span measurement `{requiredId}`.");
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

        CrossCheckMaps(documents["maps.json"].RootElement, sourceIds, failures);
    }

    private static void CrossCheckMaps(JsonElement root, HashSet<string> sourceIds, List<string> failures)
    {
        var units = root.GetProperty("units");
        if (units.GetProperty("sourcePlayerDiameterPixels").GetDouble() != 18 ||
            units.GetProperty("sourceMaskPixels").GetProperty("width").GetInt32() != 640 ||
            units.GetProperty("sourceMaskPixels").GetProperty("height").GetInt32() != 360)
        {
            failures.Add("SPEC056 maps.json unit calibration exceeds supported bounds.");
        }

        var vocabulary = root.GetProperty("geometryVocabulary").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var maps = root.GetProperty("maps").EnumerateArray().ToArray();
        if (maps.Length != root.GetProperty("catalogCount").GetInt32())
        {
            failures.Add($"SPEC040 maps.json contains {maps.Length} arenas but declares {root.GetProperty("catalogCount").GetInt32()}.");
        }

        var mapIds = new HashSet<string>(StringComparer.Ordinal);
        var mapsById = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var sourceRows = new HashSet<int>();
        foreach (var map in maps)
        {
            var mapId = map.GetProperty("id").GetString()!;
            if (!mapIds.Add(mapId))
            {
                failures.Add($"SPEC041 maps.json duplicates arena id `{mapId}`.");
                continue;
            }
            mapsById.Add(mapId, map);

            var sourceRow = map.GetProperty("sourceRow").GetInt32();
            if (sourceRow > 71)
            {
                failures.Add($"SPEC056 maps.json arena `{mapId}` source row exceeds 71.");
            }
            if (!sourceRows.Add(sourceRow))
            {
                failures.Add($"SPEC042 maps.json duplicates source row `{sourceRow}`.");
            }

            ValidateMapSource(map.GetProperty("visualEvidence").GetProperty("source").GetString()!, sourceIds, $"arena `{mapId}` visual evidence", failures);
            foreach (var source in map.GetProperty("provenance").GetProperty("sources").EnumerateArray())
            {
                ValidateMapSource(source.GetString()!, sourceIds, $"arena `{mapId}` provenance", failures);
            }

            var camera = map.GetProperty("cameraBounds");
            var collision = map.GetProperty("collisionBounds");
            if (!ValidBounds(camera) || !ValidBounds(collision) || !Bounded(camera) || !Bounded(collision))
            {
                failures.Add($"SPEC043 maps.json arena `{mapId}` has reversed camera or collision bounds.");
                continue;
            }

            if (!Contains(camera, collision))
            {
                failures.Add($"SPEC044 maps.json arena `{mapId}` collision bounds escape its camera bounds.");
            }

            var primitiveIds = new HashSet<string>(StringComparer.Ordinal);
            var primitives = map.GetProperty("primitives").EnumerateArray().ToArray();
            foreach (var primitive in primitives)
            {
                var primitiveId = primitive.GetProperty("id").GetString()!;
                if (!primitiveIds.Add(primitiveId))
                {
                    failures.Add($"SPEC045 maps.json arena `{mapId}` duplicates primitive id `{primitiveId}`.");
                }

                var primitiveKind = primitive.GetProperty("primitive").GetString()!;
                if (!vocabulary.Contains(primitiveKind))
                {
                    failures.Add($"SPEC046 maps.json arena `{mapId}` uses undeclared primitive `{primitiveKind}`.");
                }

                if (Math.Abs(primitive.GetProperty("x").GetDouble()) > 40 ||
                    Math.Abs(primitive.GetProperty("y").GetDouble()) > 40 ||
                    primitive.GetProperty("width").GetDouble() > 80 ||
                    primitive.GetProperty("height").GetDouble() > 80 ||
                    Math.Abs(primitive.GetProperty("rotationDegrees").GetDouble()) > 45)
                {
                    failures.Add($"SPEC056 maps.json arena `{mapId}` primitive `{primitiveId}` exceeds supported coordinates.");
                }
            }

            var spawnCenters = new List<(double X, double Y)>();
            var spawnRegions = map.GetProperty("spawnRegions").EnumerateArray().ToArray();
            if (spawnRegions.Length != 2)
            {
                failures.Add($"SPEC056 maps.json arena `{mapId}` must contain exactly two spawn regions.");
            }

            foreach (var spawn in spawnRegions)
            {
                if (!ValidBounds(spawn) || !Bounded(spawn) || !Contains(camera, spawn) || spawn.GetProperty("clearanceDiameters").GetDouble() > 10)
                {
                    failures.Add($"SPEC047 maps.json arena `{mapId}` has an invalid or out-of-camera spawn region.");
                    continue;
                }

                var centerX = (spawn.GetProperty("xMin").GetDouble() + spawn.GetProperty("xMax").GetDouble()) / 2;
                var centerY = (spawn.GetProperty("yMin").GetDouble() + spawn.GetProperty("yMax").GetDouble()) / 2;
                spawnCenters.Add((centerX, centerY));
                var supportId = spawn.GetProperty("supportPrimitiveId").GetString()!;
                var supported = primitives.Any(primitive =>
                {
                    if (primitive.GetProperty("id").GetString() != supportId ||
                        primitive.GetProperty("role").GetString() != "static")
                    {
                        return false;
                    }

                    var angle = primitive.GetProperty("rotationDegrees").GetDouble() * Math.PI / 180;
                    var halfWidth = primitive.GetProperty("width").GetDouble() / 2;
                    var top = primitive.GetProperty("height").GetDouble() / 2;
                    var corners = new[]
                    {
                        (X: spawn.GetProperty("xMin").GetDouble(), Y: spawn.GetProperty("yMin").GetDouble()),
                        (X: spawn.GetProperty("xMin").GetDouble(), Y: spawn.GetProperty("yMax").GetDouble()),
                        (X: spawn.GetProperty("xMax").GetDouble(), Y: spawn.GetProperty("yMin").GetDouble()),
                        (X: spawn.GetProperty("xMax").GetDouble(), Y: spawn.GetProperty("yMax").GetDouble()),
                    };
                    return corners.All(corner =>
                    {
                        var deltaX = corner.X - primitive.GetProperty("x").GetDouble();
                        var deltaY = corner.Y - primitive.GetProperty("y").GetDouble();
                        var localX = deltaX * Math.Cos(angle) + deltaY * Math.Sin(angle);
                        var localY = -deltaX * Math.Sin(angle) + deltaY * Math.Cos(angle);
                        return Math.Abs(localX) <= halfWidth + 1e-6 && localY - top >= 0.2 && localY - top <= 1.3;
                    });
                });
                if (!supported || spawn.GetProperty("yMin").GetDouble() <= map.GetProperty("killBoundaryY").GetDouble() + 1)
                {
                    failures.Add($"SPEC048 maps.json arena `{mapId}` has an unsupported or kill-bound-adjacent spawn.");
                }

                foreach (var saw in map.GetProperty("behaviorModules").EnumerateArray().Where(module => module.GetProperty("kind").GetString() == "radial-saw"))
                {
                    var distance = Math.Sqrt(Math.Pow(centerX - saw.GetProperty("x").GetDouble(), 2) + Math.Pow(centerY - saw.GetProperty("y").GetDouble(), 2));
                    if (distance < saw.GetProperty("radius").GetDouble() + 1)
                    {
                        failures.Add($"SPEC049 maps.json arena `{mapId}` places a spawn inside saw clearance.");
                    }
                }
            }

            if (spawnCenters.Count == 2 && Math.Sqrt(
                    Math.Pow(spawnCenters[1].X - spawnCenters[0].X, 2) +
                    Math.Pow(spawnCenters[1].Y - spawnCenters[0].Y, 2)) < 8)
            {
                failures.Add($"SPEC050 maps.json arena `{mapId}` separates spawn centers by less than eight player diameters.");
            }

            foreach (var module in map.GetProperty("behaviorModules").EnumerateArray())
            {
                var kind = module.GetProperty("kind").GetString()!;
                var evidenceStatus = module.GetProperty("evidenceStatus").GetString()!;
                if (!vocabulary.Contains(kind))
                {
                    failures.Add($"SPEC051 maps.json arena `{mapId}` uses undeclared behavior module `{kind}`.");
                }

                if (kind == "radial-saw" && (!module.TryGetProperty("x", out _) || !module.TryGetProperty("y", out _) || !module.TryGetProperty("radius", out _)))
                {
                    failures.Add($"SPEC052 maps.json arena `{mapId}` saw lacks position or radius.");
                }
                else if (kind != "radial-saw" && !module.TryGetProperty("bounds", out _))
                {
                    failures.Add($"SPEC053 maps.json arena `{mapId}` regional behavior lacks bounds.");
                }

                if ((module.TryGetProperty("bounds", out var moduleBounds) && (!ValidBounds(moduleBounds) || !Bounded(moduleBounds))) ||
                    (module.TryGetProperty("x", out var moduleX) && Math.Abs(moduleX.GetDouble()) > 40) ||
                    (module.TryGetProperty("y", out var moduleY) && Math.Abs(moduleY.GetDouble()) > 40) ||
                    (module.TryGetProperty("radius", out var moduleRadius) && moduleRadius.GetDouble() > 10))
                {
                    failures.Add($"SPEC056 maps.json arena `{mapId}` behavior module `{module.GetProperty("id").GetString()}` exceeds supported coordinates.");
                }

                if (evidenceStatus == "measured")
                {
                    if (kind != "moving-assembly" ||
                        module.GetProperty("timingStatus").GetString() != "partial" ||
                        !module.TryGetProperty("motionEvidence", out var motionEvidence) ||
                        !module.TryGetProperty("bounds", out var measuredBounds))
                    {
                        failures.Add($"SPEC062 maps.json arena `{mapId}` has incomplete measured motion evidence.");
                        continue;
                    }

                    ValidateMapSource(motionEvidence.GetProperty("source").GetString()!, sourceIds, $"arena `{mapId}` motion evidence", failures);
                    var movingPrimitiveIds = motionEvidence.GetProperty("primitiveIds").EnumerateArray()
                        .Select(item => item.GetString()!)
                        .ToHashSet(StringComparer.Ordinal);
                    if (movingPrimitiveIds.Count < 2 || movingPrimitiveIds.Any(id => !primitives.Any(primitive =>
                            primitive.GetProperty("id").GetString() == id &&
                            primitive.GetProperty("role").GetString() == "dynamic-visual")))
                    {
                        failures.Add($"SPEC062 maps.json arena `{mapId}` measured motion does not own two dynamic primitives.");
                    }

                    var partSize = motionEvidence.GetProperty("partSize");
                    var halfWidth = (partSize.GetProperty("width").GetDouble() + partSize.GetProperty("tolerance").GetDouble()) / 2;
                    var halfHeight = (partSize.GetProperty("height").GetDouble() + partSize.GetProperty("tolerance").GetDouble()) / 2;
                    var samples = motionEvidence.GetProperty("samples").EnumerateArray().ToArray();
                    var elapsedTicks = samples.Select(sample => sample.GetProperty("elapsedTicks").GetInt32()).ToArray();
                    var orderedSamples = elapsedTicks.Zip(elapsedTicks.Skip(1), (left, right) => right > left).All(value => value);
                    var samplesInsideBounds = samples.All(sample =>
                        sample.GetProperty("leftX").GetDouble() - halfWidth >= measuredBounds.GetProperty("xMin").GetDouble() &&
                        sample.GetProperty("leftX").GetDouble() + halfWidth <= measuredBounds.GetProperty("xMax").GetDouble() &&
                        sample.GetProperty("rightX").GetDouble() - halfWidth >= measuredBounds.GetProperty("xMin").GetDouble() &&
                        sample.GetProperty("rightX").GetDouble() + halfWidth <= measuredBounds.GetProperty("xMax").GetDouble() &&
                        sample.GetProperty("leftY").GetDouble() - halfHeight >= measuredBounds.GetProperty("yMin").GetDouble() &&
                        sample.GetProperty("leftY").GetDouble() + halfHeight <= measuredBounds.GetProperty("yMax").GetDouble() &&
                        sample.GetProperty("rightY").GetDouble() - halfHeight >= measuredBounds.GetProperty("yMin").GetDouble() &&
                        sample.GetProperty("rightY").GetDouble() + halfHeight <= measuredBounds.GetProperty("yMax").GetDouble());
                    if (!orderedSamples || !samplesInsideBounds ||
                        motionEvidence.GetProperty("observedEndpointToReversalTicks").GetInt32() > elapsedTicks[^1] ||
                        motionEvidence.GetProperty("arenaMatchCoarseIntersectionOverUnion").GetDouble() > 1 ||
                        motionEvidence.GetProperty("arenaMatchSourceCoverage").GetDouble() > 1 ||
                        !Contains(collision, measuredBounds))
                    {
                        failures.Add($"SPEC062 maps.json arena `{mapId}` has invalid measured motion samples.");
                    }
                }
                else if (module.TryGetProperty("motionEvidence", out _))
                {
                    failures.Add($"SPEC062 maps.json arena `{mapId}` attaches motion measurements to unmeasured behavior.");
                }
            }

            var killBoundary = map.GetProperty("killBoundaryY").GetDouble();
            if (killBoundary < -40 || killBoundary > 0)
            {
                failures.Add($"SPEC056 maps.json arena `{mapId}` kill boundary exceeds supported coordinates.");
            }

            var evidence = map.GetProperty("layoutEvidence");
            var sourceCells = evidence.GetProperty("sourceOccupiedCells").GetInt32();
            var renderedCells = evidence.GetProperty("renderedOccupiedCells").GetInt32();
            var intersectionCells = evidence.GetProperty("intersectionCells").GetInt32();
            var unionCells = evidence.GetProperty("unionCells").GetInt32();
            var recordedIou = evidence.GetProperty("coarseIntersectionOverUnion").GetDouble();
            var computedIou = intersectionCells / (double)unionCells;
            const int coarseGridCells = 80 * 45;
            if (sourceCells > coarseGridCells ||
                renderedCells > coarseGridCells ||
                intersectionCells > coarseGridCells ||
                unionCells > coarseGridCells ||
                intersectionCells > Math.Min(sourceCells, renderedCells) ||
                unionCells < Math.Max(sourceCells, renderedCells) ||
                unionCells != sourceCells + renderedCells - intersectionCells ||
                computedIou < 0.75 ||
                Math.Abs(recordedIou - computedIou) > 0.000001)
            {
                failures.Add($"SPEC058 maps.json arena `{mapId}` has inconsistent or sub-threshold coarse layout evidence.");
            }

            if (primitives.Length < evidence.GetProperty("sourceComponentCount").GetInt32() || primitives.Length > 96)
            {
                failures.Add($"SPEC061 maps.json arena `{mapId}` does not represent each source component within the 96-box clean-room cap.");
            }

            var renderedMask = RenderMapMask(primitives);
            if (renderedMask.OccupiedCells != renderedCells ||
                renderedMask.Sha256 != evidence.GetProperty("renderedMaskSha256").GetString())
            {
                failures.Add($"SPEC059 maps.json arena `{mapId}` rendered mask digest or count does not match its committed geometry.");
            }
        }

        var arenaIndexSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reconciliation in root.GetProperty("reconciliation").EnumerateArray())
        {
            ValidateMapSource(reconciliation.GetProperty("source").GetString()!, sourceIds, "arena reconciliation", failures);
            var relationship = reconciliation.GetProperty("relationship").GetString();
            if (relationship is "exact-preview-index" or "historical-removed-subset")
            {
                arenaIndexSources.Add(reconciliation.GetProperty("source").GetString()!);
            }
        }

        if (arenaIndexSources.Count < 2)
        {
            failures.Add("SPEC060 maps.json does not reconcile two independent public arena indexes.");
        }

        foreach (var example in root.GetProperty("representativeExamples").EnumerateObject())
        {
            var exampleId = example.Value.GetString()!;
            if (!mapsById.TryGetValue(exampleId, out var map))
            {
                failures.Add($"SPEC054 maps.json representative `{example.Name}` targets missing arena `{exampleId}`.");
                continue;
            }

            var modules = map.GetProperty("behaviorModules").EnumerateArray().ToArray();
            var matchesCategory = example.Name switch
            {
                "static" => modules.Length == 0 && map.GetProperty("primitives").EnumerateArray().Any(primitive => primitive.GetProperty("role").GetString() == "static"),
                "moving" => modules.Any(module => module.GetProperty("kind").GetString() == "moving-assembly" && module.GetProperty("evidenceStatus").GetString() == "measured"),
                "breakableCandidate" => modules.Any(module => module.GetProperty("kind").GetString() == "breakable-field" && module.GetProperty("evidenceStatus").GetString() == "visual-candidate"),
                "hazard" => modules.Any(module => module.GetProperty("kind").GetString() == "radial-saw"),
                "asymmetric" => map.GetProperty("symmetry").GetString() == "asymmetric",
                "ringOut" => map.GetProperty("ringOutFocused").GetBoolean(),
                _ => false,
            };
            if (!matchesCategory)
            {
                failures.Add($"SPEC057 maps.json representative `{example.Name}` arena `{exampleId}` does not exhibit that category.");
            }
        }
    }

    private static bool ValidBounds(JsonElement bounds) =>
        bounds.GetProperty("xMin").GetDouble() < bounds.GetProperty("xMax").GetDouble() &&
        bounds.GetProperty("yMin").GetDouble() < bounds.GetProperty("yMax").GetDouble();

    private static bool Bounded(JsonElement bounds) =>
        Math.Abs(bounds.GetProperty("xMin").GetDouble()) <= 40 &&
        Math.Abs(bounds.GetProperty("xMax").GetDouble()) <= 40 &&
        Math.Abs(bounds.GetProperty("yMin").GetDouble()) <= 40 &&
        Math.Abs(bounds.GetProperty("yMax").GetDouble()) <= 40;

    private static bool Contains(JsonElement outer, JsonElement inner) =>
        inner.GetProperty("xMin").GetDouble() >= outer.GetProperty("xMin").GetDouble() &&
        inner.GetProperty("xMax").GetDouble() <= outer.GetProperty("xMax").GetDouble() &&
        inner.GetProperty("yMin").GetDouble() >= outer.GetProperty("yMin").GetDouble() &&
        inner.GetProperty("yMax").GetDouble() <= outer.GetProperty("yMax").GetDouble();

    private static (int OccupiedCells, string Sha256) RenderMapMask(JsonElement[] primitives)
    {
        const int width = 640;
        const int height = 360;
        const double playerDiameterPixels = 18;
        var pixels = new bool[width * height];
        foreach (var primitive in primitives)
        {
            var centerX = primitive.GetProperty("x").GetDouble() * playerDiameterPixels + width / 2.0;
            var centerY = height / 2.0 - primitive.GetProperty("y").GetDouble() * playerDiameterPixels;
            var boxWidth = primitive.GetProperty("width").GetDouble() * playerDiameterPixels;
            var boxHeight = primitive.GetProperty("height").GetDouble() * playerDiameterPixels;
            var angle = -primitive.GetProperty("rotationDegrees").GetDouble() * Math.PI / 180;
            var cosine = Math.Cos(angle);
            var sine = Math.Sin(angle);
            var halfAabbX = Math.Abs(cosine) * boxWidth / 2 + Math.Abs(sine) * boxHeight / 2;
            var halfAabbY = Math.Abs(sine) * boxWidth / 2 + Math.Abs(cosine) * boxHeight / 2;
            var xMin = Math.Max(0, (int)Math.Floor(centerX - halfAabbX - 1));
            var xMax = Math.Min(width, (int)Math.Ceiling(centerX + halfAabbX + 1));
            var yMin = Math.Max(0, (int)Math.Floor(centerY - halfAabbY - 1));
            var yMax = Math.Min(height, (int)Math.Ceiling(centerY + halfAabbY + 1));
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    var deltaX = x + 0.5 - centerX;
                    var deltaY = y + 0.5 - centerY;
                    var localX = deltaX * cosine + deltaY * sine;
                    var localY = -deltaX * sine + deltaY * cosine;
                    if (Math.Abs(localX) <= boxWidth / 2 + 1e-9 && Math.Abs(localY) <= boxHeight / 2 + 1e-9)
                    {
                        pixels[y * width + x] = true;
                    }
                }
            }
        }

        var bytes = new byte[pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            bytes[index] = pixels[index] ? (byte)1 : (byte)0;
        }

        var occupiedCells = 0;
        for (var coarseY = 0; coarseY < height; coarseY += 8)
        {
            for (var coarseX = 0; coarseX < width; coarseX += 8)
            {
                var occupied = false;
                for (var y = coarseY; y < coarseY + 8 && !occupied; y++)
                {
                    for (var x = coarseX; x < coarseX + 8; x++)
                    {
                        if (pixels[y * width + x])
                        {
                            occupied = true;
                            break;
                        }
                    }
                }

                if (occupied)
                {
                    occupiedCells++;
                }
            }
        }

        return (occupiedCells, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static void ValidateMapSource(string sourceId, HashSet<string> sourceIds, string subject, List<string> failures)
    {
        if (!sourceIds.Contains(sourceId))
        {
            failures.Add($"SPEC055 maps.json {subject} cites unknown source `{sourceId}`.");
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

    private static void ValidateProjectileFrameSpan(JsonElement measurement, string measurementId, List<string> failures)
    {
        var requiredTopLevelFields = new[]
        {
            "sourceEndTimestamp",
            "sourceSha256",
            "sourceFrameIndexConvention",
        };
        var missingTopLevelFields = requiredTopLevelFields
            .Where(field => !measurement.TryGetProperty(field, out _))
            .ToArray();
        var pixelMeasurements = measurement.GetProperty("pixelMeasurements");
        var requiredRawFields = new[]
        {
            "firstSourceFrame",
            "lastSourceFrame",
            "elapsedFrames",
            "sourceWidthPixels",
            "sourceHeightPixels",
            "sourceFps",
            "ticksPerSourceFrame",
            "projectileStartCenterX",
            "projectileStartCenterY",
            "projectileEndCenterX",
            "projectileEndCenterY",
            "stableReferenceStartX",
            "stableReferenceStartY",
            "stableReferenceEndX",
            "stableReferenceEndY",
            "euclideanDisplacementPixels",
            "playerDiameterPixels",
            "coreCenterUncertaintyPixels",
            "stableReferenceUncertaintyPixels",
            "playerDiameterUncertaintyPixels",
            "frameTimingUncertaintyFrames",
            "intervalMinimum",
            "intervalMaximum",
        };
        var missingRawFields = requiredRawFields
            .Where(field => !pixelMeasurements.TryGetProperty(field, out _))
            .ToArray();
        if (missingTopLevelFields.Length > 0 || missingRawFields.Length > 0)
        {
            var missing = missingTopLevelFields.Concat(missingRawFields.Select(field => $"pixelMeasurements.{field}"));
            failures.Add($"SPEC057 measurements.json measurement `{measurementId}` omits required projectile frame-span field(s): {string.Join(", ", missing)}.");
            return;
        }

        var firstFrame = pixelMeasurements.GetProperty("firstSourceFrame").GetInt32();
        var lastFrame = pixelMeasurements.GetProperty("lastSourceFrame").GetInt32();
        var elapsedFrames = pixelMeasurements.GetProperty("elapsedFrames").GetDouble();
        var expectedElapsedFrames = lastFrame - firstFrame;
        if (elapsedFrames <= 1 || Math.Abs(elapsedFrames - expectedElapsedFrames) > 0.0001)
        {
            failures.Add($"SPEC058 measurements.json measurement `{measurementId}` records elapsedFrames as {elapsedFrames} but source-frame endpoints require {expectedElapsedFrames}.");
        }

        if (!RequiredProjectileFrameSpans.TryGetValue(measurementId, out var requiredFrames) ||
            firstFrame != requiredFrames.FirstFrame ||
            lastFrame != requiredFrames.LastFrame ||
            measurement.GetProperty("metricFactId").GetString() != "combat-projectile-speed" ||
            measurement.GetProperty("source").GetString() != "footage-ssag" ||
            measurement.GetProperty("sourceTimestamp").GetString() != requiredFrames.FirstTimestamp ||
            measurement.GetProperty("sourceEndTimestamp").GetString() != requiredFrames.LastTimestamp ||
            measurement.GetProperty("sourceSha256").GetString() != ProjectileSourceSha256 ||
            !measurement.GetProperty("countsTowardCoverage").GetBoolean() ||
            !measurement.GetProperty("activeCards").EnumerateArray().Select(card => card.GetString()).SequenceEqual(new[] { "Tank", "Leech" }, StringComparer.Ordinal) ||
            pixelMeasurements.GetProperty("sourceWidthPixels").GetInt32() != 640 ||
            pixelMeasurements.GetProperty("sourceHeightPixels").GetInt32() != 360 ||
            pixelMeasurements.GetProperty("sourceFps").GetDouble() != 30 ||
            pixelMeasurements.GetProperty("ticksPerSourceFrame").GetDouble() != 2)
        {
            failures.Add($"SPEC061 measurements.json measurement `{measurementId}` does not match its fixed retained-source frame-span contract.");
        }

        var ticksPerSourceFrame = pixelMeasurements.GetProperty("ticksPerSourceFrame").GetDouble();
        var observedTicks = measurement.GetProperty("observedFrameIntervalTicks").GetDouble();
        if (Math.Abs(observedTicks - ticksPerSourceFrame) > 0.0001)
        {
            failures.Add($"SPEC058 measurements.json measurement `{measurementId}` records {observedTicks} ticks per source-frame interval but cadence requires {ticksPerSourceFrame:F6}.");
        }

        var derivation = measurement.GetProperty("derivation");
        var operation = derivation.GetProperty("operation").GetString();
        var operands = derivation.GetProperty("operands")
            .EnumerateArray()
            .Select(operand => operand.GetString()!)
            .ToArray();
        var requiredOperands = new[]
        {
            "pixelMeasurements.euclideanDisplacementPixels",
            "pixelMeasurements.playerDiameterPixels",
            "pixelMeasurements.elapsedFrames",
            "pixelMeasurements.ticksPerSourceFrame",
        };
        if (operation != "divide" || !operands.SequenceEqual(requiredOperands, StringComparer.Ordinal))
        {
            failures.Add($"SPEC058 measurements.json measurement `{measurementId}` must divide by player diameter, elapsedFrames, and ticksPerSourceFrame in that order.");
        }

        var evidence = new ProjectileFrameSpanEvidence(
            pixelMeasurements.GetProperty("projectileStartCenterX").GetDouble(),
            pixelMeasurements.GetProperty("projectileStartCenterY").GetDouble(),
            pixelMeasurements.GetProperty("projectileEndCenterX").GetDouble(),
            pixelMeasurements.GetProperty("projectileEndCenterY").GetDouble(),
            pixelMeasurements.GetProperty("stableReferenceStartX").GetDouble(),
            pixelMeasurements.GetProperty("stableReferenceStartY").GetDouble(),
            pixelMeasurements.GetProperty("stableReferenceEndX").GetDouble(),
            pixelMeasurements.GetProperty("stableReferenceEndY").GetDouble(),
            pixelMeasurements.GetProperty("playerDiameterPixels").GetDouble(),
            elapsedFrames,
            ticksPerSourceFrame,
            pixelMeasurements.GetProperty("coreCenterUncertaintyPixels").GetDouble(),
            pixelMeasurements.GetProperty("stableReferenceUncertaintyPixels").GetDouble(),
            pixelMeasurements.GetProperty("playerDiameterUncertaintyPixels").GetDouble(),
            pixelMeasurements.GetProperty("frameTimingUncertaintyFrames").GetDouble());
        var recordedDistance = pixelMeasurements.GetProperty("euclideanDisplacementPixels").GetDouble();
        if (Math.Abs(evidence.EuclideanDisplacementPixels - recordedDistance) > 0.0001)
        {
            failures.Add($"SPEC059 measurements.json measurement `{measurementId}` records Euclidean displacement as {recordedDistance} but its camera-compensated endpoints recompute to {evidence.EuclideanDisplacementPixels:F6}.");
        }

        var interval = evidence.NormalizedSpeedInterval();
        var recordedMinimum = pixelMeasurements.GetProperty("intervalMinimum").GetDouble();
        var recordedMaximum = pixelMeasurements.GetProperty("intervalMaximum").GetDouble();
        if (!double.IsFinite(interval.Minimum) ||
            !double.IsFinite(interval.Maximum) ||
            Math.Abs(interval.Minimum - recordedMinimum) > 0.000001 ||
            Math.Abs(interval.Maximum - recordedMaximum) > 0.000001)
        {
            failures.Add($"SPEC060 measurements.json measurement `{measurementId}` records interval [{recordedMinimum}, {recordedMaximum}] but uncertainty recomputes to [{interval.Minimum:F6}, {interval.Maximum:F6}].");
        }
    }
}
