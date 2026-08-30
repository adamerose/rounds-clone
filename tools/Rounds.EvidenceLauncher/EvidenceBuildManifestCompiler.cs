using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Rounds.EvidenceLauncher;

internal enum EvidenceManifestRecordKind : byte
{
    Directory = 1,
    File = 2,
}

internal enum EvidenceManifestContentKind : byte
{
    RawBytes = 1,
    RepositoryAndPackageRootNormalizedProjectAssetsJson = 2,
}

internal sealed class EvidenceManifestEntry
{
    private readonly ReadOnlyCollection<byte> _content;

    internal EvidenceManifestEntry(
        string relativePath,
        EvidenceManifestRecordKind kind,
        IEnumerable<byte>? content = null,
        EvidenceManifestContentKind contentKind = EvidenceManifestContentKind.RawBytes)
    {
        RelativePath = EvidenceBuildManifestCompiler.NormalizeRelativePath(relativePath);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(contentKind)) throw new ArgumentOutOfRangeException(nameof(contentKind));
        Kind = kind;
        ContentKind = contentKind;
        var frozen = FreezeContent(content);
        if (kind == EvidenceManifestRecordKind.Directory && frozen.Length != 0)
        {
            throw new ArgumentException("Directory manifest records cannot contain bytes.", nameof(content));
        }
        if (kind == EvidenceManifestRecordKind.Directory && contentKind != EvidenceManifestContentKind.RawBytes)
        {
            throw new ArgumentException("Directory manifest records cannot request a content transform.", nameof(contentKind));
        }
        _content = Array.AsReadOnly(frozen);
    }

    internal string RelativePath { get; }

    internal EvidenceManifestRecordKind Kind { get; }

    internal EvidenceManifestContentKind ContentKind { get; }

    internal IReadOnlyList<byte> Content => _content;

    private static byte[] FreezeContent(IEnumerable<byte>? content)
    {
        if (content is null) return [];
        if (content is ICollection<byte> collection &&
            collection.Count > EvidenceBuildManifestCompiler.MaximumEntryContentBytes)
        {
            throw new ArgumentException("Manifest entry content exceeded its bound.", nameof(content));
        }
        var frozen = new List<byte>();
        foreach (var value in content)
        {
            if (frozen.Count == EvidenceBuildManifestCompiler.MaximumEntryContentBytes)
            {
                throw new ArgumentException("Manifest entry content exceeded its bound.", nameof(content));
            }
            frozen.Add(value);
        }
        return frozen.ToArray();
    }
}

internal sealed class EvidenceManifestDefinition
{
    private readonly ReadOnlyCollection<EvidenceManifestEntry> _requiredEntries;
    private readonly ReadOnlyCollection<string> _excludedPaths;

    internal EvidenceManifestDefinition(
        IEnumerable<EvidenceManifestEntry> requiredEntries,
        IEnumerable<string>? excludedPaths = null)
    {
        ArgumentNullException.ThrowIfNull(requiredEntries);
        var required = requiredEntries.Take(EvidenceBuildManifestCompiler.MaximumEntryCount + 1)
            .Select(Clone).OrderBy(value => value.RelativePath, StringComparer.Ordinal).ToArray();
        if (required.Length == 0) throw new ArgumentException("A manifest definition cannot be empty.", nameof(requiredEntries));
        if (required.Length > EvidenceBuildManifestCompiler.MaximumEntryCount)
        {
            throw new ArgumentException("Manifest definition entry count exceeded its bound.", nameof(requiredEntries));
        }
        EvidenceBuildManifestCompiler.RequireUniquePaths(required.Select(value => value.RelativePath), "required manifest");

        var excluded = (excludedPaths ?? []).Take(EvidenceBuildManifestCompiler.MaximumEntryCount + 1)
            .Select(EvidenceBuildManifestCompiler.NormalizeRelativePath)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (excluded.Length > EvidenceBuildManifestCompiler.MaximumEntryCount)
        {
            throw new ArgumentException("Excluded manifest entry count exceeded its bound.", nameof(excludedPaths));
        }
        EvidenceBuildManifestCompiler.RequireUniquePaths(excluded, "excluded manifest");
        var requiredCaseInsensitive = required.Select(value => value.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (excluded.Any(requiredCaseInsensitive.Contains))
        {
            throw new ArgumentException("Required and excluded manifest paths overlap.", nameof(excludedPaths));
        }

        _requiredEntries = Array.AsReadOnly(required);
        _excludedPaths = Array.AsReadOnly(excluded);
    }

    internal IReadOnlyList<EvidenceManifestEntry> RequiredEntries => _requiredEntries;

    internal IReadOnlyList<string> ExcludedPaths => _excludedPaths;

    private static EvidenceManifestEntry Clone(EvidenceManifestEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EvidenceManifestEntry(value.RelativePath, value.Kind, value.Content, value.ContentKind);
    }
}

internal sealed record EvidenceCompiledManifestEntry(
    string RelativePath,
    EvidenceManifestRecordKind Kind,
    EvidenceManifestContentKind ContentKind,
    long ContentLength,
    string ContentSha256);

internal sealed record EvidenceCompiledManifest(
    string Algorithm,
    string Sha256,
    IReadOnlyList<EvidenceCompiledManifestEntry> Entries);

internal static class EvidenceBuildManifestCompiler
{
    internal const string Algorithm = "rounds-evidence-manifest-sha256-length-prefixed-v1";
    internal const int MaximumEntryCount = 65_536;
    internal const int MaximumPathUtf8Bytes = 1_024;
    internal const int MaximumEntryContentBytes = 256 * 1024 * 1024;
    internal const long MaximumTotalContentBytes = 512L * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Domain = StrictUtf8.GetBytes("ROUNDS-EVIDENCE-MANIFEST\0V1");

    internal static EvidenceCompiledManifest CompileExact(
        EvidenceManifestDefinition definition,
        IEnumerable<EvidenceManifestEntry> observedEntries,
        string exactRepositoryRoot,
        string exactPackageRoot)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(observedEntries);
        var repositoryRoot = NormalizeAbsoluteRoot(exactRepositoryRoot, nameof(exactRepositoryRoot));
        var packageRoot = NormalizeAbsoluteRoot(exactPackageRoot, nameof(exactPackageRoot));
        if (string.Equals(repositoryRoot, packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Repository and package roots must be distinct.");
        }

        var observed = observedEntries.Take(MaximumEntryCount + 1).Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            return new EvidenceManifestEntry(value.RelativePath, value.Kind, value.Content, value.ContentKind);
        }).OrderBy(value => value.RelativePath, StringComparer.Ordinal).ToArray();
        if (observed.Length > MaximumEntryCount) throw new InvalidOperationException("Manifest entry count exceeded its bound.");
        RequireUniquePaths(observed.Select(value => value.RelativePath), "observed manifest");

        var excluded = definition.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedObserved = observed.FirstOrDefault(value => excluded.Contains(value.RelativePath));
        if (excludedObserved is not null)
        {
            throw new InvalidOperationException($"Excluded manifest path was observed: {excludedObserved.RelativePath}");
        }

        if (definition.RequiredEntries.Count != observed.Length)
        {
            throw new InvalidOperationException("Observed manifest did not contain exactly the required entries.");
        }
        for (var index = 0; index < observed.Length; index++)
        {
            var expected = definition.RequiredEntries[index];
            var actual = observed[index];
            if (!string.Equals(expected.RelativePath, actual.RelativePath, StringComparison.Ordinal) ||
                expected.Kind != actual.Kind || expected.ContentKind != actual.ContentKind)
            {
                throw new InvalidOperationException("Observed manifest entry path, kind, or content transform drifted.");
            }
        }

        using var manifestHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(manifestHash, Domain);
        AppendUnsigned(manifestHash, checked((ulong)observed.Length));
        var compiled = new EvidenceCompiledManifestEntry[observed.Length];
        long totalBytes = 0;
        for (var index = 0; index < observed.Length; index++)
        {
            var entry = observed[index];
            var pathBytes = StrictUtf8.GetBytes(entry.RelativePath);
            if (pathBytes.Length > MaximumPathUtf8Bytes) throw new InvalidOperationException("Manifest path exceeded its UTF-8 bound.");
            byte[] content = entry.Content.ToArray();
            if (content.Length > MaximumEntryContentBytes) throw new InvalidOperationException("Manifest entry content exceeded its bound.");
            if (entry.ContentKind == EvidenceManifestContentKind.RepositoryAndPackageRootNormalizedProjectAssetsJson)
            {
                content = NormalizeProjectAssetsJson(content, repositoryRoot, packageRoot);
            }
            totalBytes = checked(totalBytes + content.Length);
            if (totalBytes > MaximumTotalContentBytes) throw new InvalidOperationException("Manifest total content exceeded its bound.");

            AppendField(manifestHash, [(byte)entry.Kind]);
            AppendField(manifestHash, pathBytes);
            AppendField(manifestHash, [(byte)entry.ContentKind]);
            AppendField(manifestHash, content);
            compiled[index] = new EvidenceCompiledManifestEntry(
                entry.RelativePath,
                entry.Kind,
                entry.ContentKind,
                content.LongLength,
                Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
        }

        return new EvidenceCompiledManifest(
            Algorithm,
            Convert.ToHexString(manifestHash.GetHashAndReset()).ToLowerInvariant(),
            Array.AsReadOnly(compiled));
    }

    internal static string NormalizeRelativePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.IndexOf('\0') >= 0 || value.Contains(':', StringComparison.Ordinal) ||
            Path.IsPathRooted(value) || value.StartsWith("//", StringComparison.Ordinal) ||
            value.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Manifest paths must be ordinary relative paths.", nameof(value));
        }
        var replaced = value.Replace('\\', '/');
        var segments = replaced.Split('/');
        if (segments.Length == 0 || segments.Any(segment => !ValidSegment(segment)))
        {
            throw new ArgumentException("Manifest path contains a non-canonical segment.", nameof(value));
        }
        var normalized = string.Join('/', segments);
        var utf8Length = StrictUtf8.GetByteCount(normalized);
        if (utf8Length > MaximumPathUtf8Bytes) throw new ArgumentException("Manifest path exceeded its UTF-8 bound.", nameof(value));
        return normalized;
    }

    internal static byte[] NormalizeProjectAssetsJson(
        IReadOnlyList<byte> bytes,
        string exactRepositoryRoot,
        string exactPackageRoot)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Count > MaximumEntryContentBytes)
        {
            throw new InvalidDataException("Project assets JSON exceeded its byte bound.");
        }
        var repositoryRoot = NormalizeAbsoluteRoot(exactRepositoryRoot, nameof(exactRepositoryRoot));
        var packageRoot = NormalizeAbsoluteRoot(exactPackageRoot, nameof(exactPackageRoot));
        var source = bytes.ToArray();
        if (source.Length >= 3 && source[0] == 0xef && source[1] == 0xbb && source[2] == 0xbf)
        {
            throw new InvalidDataException("Project assets JSON must not contain a UTF-8 BOM.");
        }
        _ = StrictUtf8.GetString(source); // validates the entire byte sequence, including ignored JSON whitespace
        using var document = JsonDocument.Parse(source, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Project assets JSON root must be an object.");
        }
        ValidateNoDuplicateProperties(document.RootElement);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            WriteNormalizedJson(writer, document.RootElement, repositoryRoot, packageRoot, jsonPath: "$");
        }
        return output.ToArray();
    }

    internal static void RequireUniquePaths(IEnumerable<string> paths, string label)
    {
        var ordinal = new HashSet<string>(StringComparer.Ordinal);
        var caseInsensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!ordinal.Add(path)) throw new ArgumentException($"Duplicate {label} path: {path}");
            if (!caseInsensitive.Add(path)) throw new ArgumentException($"Case-colliding {label} path: {path}");
        }
    }

    private static void AppendField(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(length, checked((ulong)bytes.Length));
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void AppendUnsigned(IncrementalHash hash, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static bool ValidSegment(string segment)
    {
        if (segment.Length == 0 || segment is "." or ".." ||
            segment.EndsWith(' ') || segment.EndsWith('.') ||
            segment.Any(character => char.IsControl(character) || character is '<' or '>' or '"' or '|' or '?' or '*'))
        {
            return false;
        }
        try { _ = StrictUtf8.GetByteCount(segment); }
        catch (EncoderFallbackException) { return false; }
        var stem = segment.Split('.')[0];
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        return !reserved.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeAbsoluteRoot(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!IsOrdinaryAbsolutePath(value))
        {
            throw new ArgumentException("Manifest normalization roots must be canonical absolute paths.", parameterName);
        }
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (!string.Equals(value, full, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Manifest normalization roots must already be canonical.", parameterName);
        }
        return full;
    }

    private static void ValidateNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var caseInsensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!exact.Add(property.Name) || !caseInsensitive.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate or case-colliding JSON property: {property.Name}");
                }
                ValidateNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) ValidateNoDuplicateProperties(item);
        }
    }

    private static void WriteNormalizedJson(
        Utf8JsonWriter writer,
        JsonElement element,
        string repositoryRoot,
        string packageRoot,
        string jsonPath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    var name = jsonPath == "$/packageFolders"
                        ? NormalizeExactRootPropertyName(property.Name, packageRoot)
                        : property.Name;
                    writer.WritePropertyName(name);
                    WriteNormalizedPropertyValue(
                        writer, property.Name, property.Value, repositoryRoot, packageRoot,
                        jsonPath, jsonPath + "/" + EscapeJsonPathSegment(property.Name));
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteNormalizedJson(writer, item, repositoryRoot, packageRoot, jsonPath + "/[]");
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("Unsupported JSON token in project assets.");
        }
    }

    private static void WriteNormalizedPropertyValue(
        Utf8JsonWriter writer,
        string propertyName,
        JsonElement value,
        string repositoryRoot,
        string packageRoot,
        string parentPath,
        string valuePath)
    {
        if (value.ValueKind == JsonValueKind.String && IsRepositoryPathField(parentPath, propertyName))
        {
            writer.WriteStringValue(NormalizeRepositoryValue(value.GetString()!, repositoryRoot));
            return;
        }
        if (value.ValueKind == JsonValueKind.String &&
            parentPath == "$/project/restore" && propertyName == "packagesPath")
        {
            writer.WriteStringValue(NormalizeExactPackageValue(value.GetString()!, packageRoot));
            return;
        }
        WriteNormalizedJson(writer, value, repositoryRoot, packageRoot, valuePath);
    }

    private static bool IsRepositoryPathField(string parentPath, string propertyName)
    {
        if (parentPath == "$/project/restore" &&
            propertyName is "projectUniqueName" or "projectPath" or "outputPath")
        {
            return true;
        }
        if (propertyName != "projectPath") return false;
        var segments = parentPath.Split('/');
        return segments.Length == 6 && segments[0] == "$" && segments[1] == "project" &&
            segments[2] == "frameworks" && segments[4] == "projectReferences";
    }

    private static string EscapeJsonPathSegment(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string NormalizeRepositoryValue(string value, string repositoryRoot)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(value);
        if (!IsOrdinaryAbsolutePath(trimmed)) throw new InvalidDataException("Named repository path was not an ordinary absolute path.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        if (!string.Equals(trimmed, full, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Named repository path was not canonical.");
        }
        if (string.Equals(full, repositoryRoot, StringComparison.OrdinalIgnoreCase)) return "${REPOSITORY_ROOT}";
        var prefix = repositoryRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Named repository path escaped the exact repository root.");
        }
        return "${REPOSITORY_ROOT}/" + Path.GetRelativePath(repositoryRoot, full).Replace('\\', '/');
    }

    private static string NormalizeExactPackageValue(string value, string packageRoot)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(value);
        if (!IsOrdinaryAbsolutePath(trimmed)) throw new InvalidDataException("Named package path was not an ordinary absolute path.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        if (!string.Equals(trimmed, full, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(full, packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Named package path did not equal the exact package root.");
        }
        return "${PACKAGE_ROOT}";
    }

    private static string NormalizeExactRootPropertyName(string propertyName, string packageRoot)
    {
        _ = NormalizeExactPackageValue(propertyName, packageRoot);
        return "${PACKAGE_ROOT}/";
    }

    private static bool IsOrdinaryAbsolutePath(string value)
    {
        if (value.IndexOf('\0') >= 0 || value.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            value.StartsWith(@"\\.\", StringComparison.Ordinal) || !Path.IsPathFullyQualified(value))
        {
            return false;
        }
        if (value.StartsWith(@"\\", StringComparison.Ordinal)) return value.IndexOf(':') < 0;
        return value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' &&
            (value[2] == '\\' || value[2] == '/') && value.IndexOf(':', 2) < 0;
    }
}
