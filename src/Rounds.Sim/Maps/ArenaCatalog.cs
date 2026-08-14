using System.Text.Json;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Maps;

public sealed class ArenaCatalog
{
    private const string EmbeddedResourceName = "Rounds.Sim.Data.maps.json";
    private readonly ArenaDefinition[] _arenas;

    private ArenaCatalog(ArenaDefinition[] arenas)
    {
        _arenas = arenas;
    }

    public IReadOnlyList<ArenaDefinition> Arenas => Array.AsReadOnly(_arenas);

    public static ArenaCatalog LoadEmbedded()
    {
        var assembly = typeof(ArenaCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded arena catalog `{EmbeddedResourceName}` is missing.");
        return Load(stream);
    }

    public static ArenaCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("targetBuild").GetString() != "21020021")
        {
            throw new InvalidDataException("Arena catalog targets an unsupported build.");
        }

        var arenas = new List<ArenaDefinition>();
        foreach (var map in root.GetProperty("maps").EnumerateArray())
        {
            try
            {
                arenas.Add(ParseArena(map));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Arena catalog contains an invalid arena definition.", exception);
            }
        }

        if (arenas.Count != root.GetProperty("catalogCount").GetInt32())
        {
            throw new InvalidDataException("Arena catalog count does not match its map array.");
        }

        for (var index = 0; index < arenas.Count; index++)
        {
            if (arenas.Take(index).Any(existing => existing.Id == arenas[index].Id))
            {
                throw new InvalidDataException($"Arena catalog duplicates `{arenas[index].Id}`.");
            }
        }

        return new ArenaCatalog(arenas.ToArray());
    }

    public ArenaDefinition GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (var arena in _arenas)
        {
            if (string.Equals(arena.Id, id, StringComparison.Ordinal))
            {
                return arena;
            }
        }

        throw new KeyNotFoundException($"Arena `{id}` is not present in the catalog.");
    }

    private static ArenaDefinition ParseArena(JsonElement map)
    {
        var id = map.GetProperty("id").GetString()
            ?? throw new InvalidDataException("Arena id is missing.");
        var boxes = new List<Obb>();
        var sourceOrder = 0;
        foreach (var primitive in map.GetProperty("primitives").EnumerateArray())
        {
            if (primitive.GetProperty("primitive").GetString() != "oriented-box")
            {
                throw new InvalidDataException($"Arena `{id}` has an unsupported primitive.");
            }

            switch (primitive.GetProperty("role").GetString())
            {
                case "static":
                    boxes.Add(Obb.Create(
                        primitive.GetProperty("id").GetString()!,
                        sourceOrder,
                        new Vec2(
                            primitive.GetProperty("x").GetDouble(),
                            primitive.GetProperty("y").GetDouble()),
                        primitive.GetProperty("width").GetDouble(),
                        primitive.GetProperty("height").GetDouble(),
                        primitive.GetProperty("rotationDegrees").GetDouble()));
                    break;
                case "hazard-visual":
                case "dynamic-visual":
                    break;
                default:
                    throw new InvalidDataException($"Arena `{id}` has an unsupported primitive role.");
            }

            sourceOrder++;
        }

        var spawns = map.GetProperty("spawnRegions").EnumerateArray()
            .Select(spawn => new SpawnRegion(
                spawn.GetProperty("id").GetString()!,
                ParseBounds(spawn),
                spawn.GetProperty("supportPrimitiveId").GetString()!))
            .ToArray();
        return new ArenaDefinition(
            id,
            ParseBounds(map.GetProperty("cameraBounds")),
            ParseBounds(map.GetProperty("collisionBounds")),
            map.GetProperty("killBoundaryY").GetDouble(),
            boxes,
            spawns);
    }

    private static ArenaBounds ParseBounds(JsonElement element)
    {
        var bounds = new ArenaBounds(
            element.GetProperty("xMin").GetDouble(),
            element.GetProperty("xMax").GetDouble(),
            element.GetProperty("yMin").GetDouble(),
            element.GetProperty("yMax").GetDouble());
        if (!bounds.IsValid)
        {
            throw new InvalidDataException("Arena catalog contains invalid bounds.");
        }

        return bounds;
    }
}
