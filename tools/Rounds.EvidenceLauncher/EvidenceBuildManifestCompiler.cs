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

internal readonly record struct EvidenceManifestCompilationLimits(
    int MaximumEntries,
    int MaximumPathBytes,
    int MaximumEntryBytes,
    long MaximumTotalBytes);

internal interface IEvidenceManifestEntrySource
{
    string RelativePath { get; }

    EvidenceManifestRecordKind Kind { get; }

    EvidenceManifestContentKind ContentKind { get; }

    long ContentLength { get; }

    byte[] CloneContent();
}

internal interface IEvidenceProjectAssetsNormalizer
{
    byte[] Normalize(
        IReadOnlyList<byte> bytes,
        string exactRepositoryRoot,
        string exactPackageRoot,
        int maximumOutputBytes);
}

internal sealed class EvidenceManifestEntry : IEvidenceManifestEntrySource
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

    public string RelativePath { get; }

    public EvidenceManifestRecordKind Kind { get; }

    public EvidenceManifestContentKind ContentKind { get; }

    internal IReadOnlyList<byte> Content => _content;

    public long ContentLength => _content.Count;

    public byte[] CloneContent() => _content.ToArray();

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
        : this(requiredEntries.Cast<IEvidenceManifestEntrySource>(), excludedPaths,
            EvidenceBuildManifestCompiler.DefaultLimits)
    {
    }

    private EvidenceManifestDefinition(
        IEnumerable<IEvidenceManifestEntrySource> requiredEntries,
        IEnumerable<string>? excludedPaths,
        EvidenceManifestCompilationLimits limits)
    {
        ArgumentNullException.ThrowIfNull(requiredEntries);
        EvidenceBuildManifestCompiler.ValidateLimits(limits);
        var required = EvidenceBuildManifestCompiler.FreezeSources(requiredEntries, limits, "manifest definition");
        if (required.Length == 0) throw new ArgumentException("A manifest definition cannot be empty.", nameof(requiredEntries));
        EvidenceBuildManifestCompiler.RequireUniquePaths(required.Select(value => value.RelativePath), "required manifest");

        var excluded = (excludedPaths ?? []).Take(limits.MaximumEntries + 1)
            .Select(EvidenceBuildManifestCompiler.NormalizeRelativePath)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (excluded.Length > limits.MaximumEntries)
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

    internal static EvidenceManifestDefinition CreateForSources(
        IEnumerable<IEvidenceManifestEntrySource> requiredEntries,
        IEnumerable<string>? excludedPaths,
        EvidenceManifestCompilationLimits limits) =>
        new(requiredEntries, excludedPaths, limits);
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
    internal static readonly EvidenceManifestCompilationLimits DefaultLimits = new(
        MaximumEntryCount,
        MaximumPathUtf8Bytes,
        MaximumEntryContentBytes,
        MaximumTotalContentBytes);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Domain = StrictUtf8.GetBytes("ROUNDS-EVIDENCE-MANIFEST\0V1");
    private static readonly IEvidenceProjectAssetsNormalizer ProjectAssetsNormalizer =
        new StrictProjectAssetsNormalizer();

    internal static EvidenceCompiledManifest CompileExact(
        EvidenceManifestDefinition definition,
        IEnumerable<EvidenceManifestEntry> observedEntries,
        string exactRepositoryRoot,
        string exactPackageRoot) =>
        CompileExactForSources(
            definition,
            observedEntries.Cast<IEvidenceManifestEntrySource>(),
            exactRepositoryRoot,
            exactPackageRoot,
            DefaultLimits,
            ProjectAssetsNormalizer);

    internal static EvidenceCompiledManifest CompileExactForSources(
        EvidenceManifestDefinition definition,
        IEnumerable<IEvidenceManifestEntrySource> observedEntries,
        string exactRepositoryRoot,
        string exactPackageRoot,
        EvidenceManifestCompilationLimits limits,
        IEvidenceProjectAssetsNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(observedEntries);
        ArgumentNullException.ThrowIfNull(normalizer);
        ValidateLimits(limits);
        var repositoryRoot = NormalizeAbsoluteRoot(exactRepositoryRoot, nameof(exactRepositoryRoot));
        var packageRoot = NormalizeAbsoluteRoot(exactPackageRoot, nameof(exactPackageRoot));
        if (string.Equals(repositoryRoot, packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Repository and package roots must be distinct.");
        }

        var observed = PrepareSources(observedEntries, limits, "observed manifest");
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
            if (pathBytes.Length > limits.MaximumPathBytes) throw new InvalidOperationException("Manifest path exceeded its UTF-8 bound.");
            var remainingBeforeClone = checked(limits.MaximumTotalBytes - totalBytes);
            if (entry.ContentLength > remainingBeforeClone)
            {
                throw new InvalidOperationException("Manifest total content exceeded its bound before entry cloning.");
            }
            byte[] content = entry.Source.CloneContent();
            if (content.LongLength != entry.ContentLength)
            {
                throw new InvalidOperationException("Observed manifest entry length changed while it was cloned.");
            }
            if (content.Length > limits.MaximumEntryBytes) throw new InvalidOperationException("Manifest entry content exceeded its bound.");
            if (entry.ContentKind == EvidenceManifestContentKind.RepositoryAndPackageRootNormalizedProjectAssetsJson)
            {
                var remaining = checked(limits.MaximumTotalBytes - totalBytes);
                var outputLimit = checked((int)Math.Min(limits.MaximumEntryBytes, remaining));
                content = normalizer.Normalize(content, repositoryRoot, packageRoot, outputLimit);
                if (content.Length > outputLimit)
                {
                    throw new InvalidOperationException("Normalized manifest entry exceeded its supplied output bound.");
                }
            }
            totalBytes = checked(totalBytes + content.Length);
            if (totalBytes > limits.MaximumTotalBytes) throw new InvalidOperationException("Manifest total content exceeded its bound.");

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

    internal static EvidenceManifestEntry[] FreezeSources(
        IEnumerable<IEvidenceManifestEntrySource> sources,
        EvidenceManifestCompilationLimits limits,
        string label)
    {
        var prepared = PrepareSources(sources, limits, label);
        var frozen = new List<EvidenceManifestEntry>();
        foreach (var item in prepared)
        {
            var content = item.Source.CloneContent();
            if (content.LongLength != item.ContentLength)
            {
                throw new InvalidOperationException($"{label} entry length changed while it was cloned.");
            }
            frozen.Add(new EvidenceManifestEntry(item.RelativePath, item.Kind, content, item.ContentKind));
        }
        return frozen.ToArray();
    }

    private static PreparedManifestSource[] PrepareSources(
        IEnumerable<IEvidenceManifestEntrySource> sources,
        EvidenceManifestCompilationLimits limits,
        string label)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ValidateLimits(limits);
        var prepared = new List<PreparedManifestSource>();
        long totalBytes = 0;
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            var relativePath = source.RelativePath;
            var kind = source.Kind;
            var contentKind = source.ContentKind;
            var contentLength = source.ContentLength;
            if (prepared.Count == limits.MaximumEntries)
            {
                throw new InvalidOperationException($"{label} entry count exceeded its bound.");
            }
            if (contentLength < 0 || contentLength > limits.MaximumEntryBytes)
            {
                throw new InvalidOperationException($"{label} entry content exceeded its bound.");
            }
            if (!Enum.IsDefined(kind) || !Enum.IsDefined(contentKind))
            {
                throw new InvalidOperationException($"{label} entry kind was invalid.");
            }
            if (kind == EvidenceManifestRecordKind.Directory &&
                (contentLength != 0 || contentKind != EvidenceManifestContentKind.RawBytes))
            {
                throw new InvalidOperationException(
                    $"{label} directory entries must be empty raw-byte records.");
            }
            var normalizedPath = NormalizeRelativePath(relativePath);
            if (StrictUtf8.GetByteCount(normalizedPath) > limits.MaximumPathBytes)
            {
                throw new InvalidOperationException($"{label} path exceeded its UTF-8 bound.");
            }
            if (contentLength > limits.MaximumTotalBytes - totalBytes)
            {
                throw new InvalidOperationException($"{label} total content exceeded its bound.");
            }
            totalBytes = checked(totalBytes + contentLength);
            prepared.Add(new PreparedManifestSource(
                source, normalizedPath, kind, contentKind, contentLength));
        }
        return prepared.OrderBy(value => value.RelativePath, StringComparer.Ordinal).ToArray();
    }

    internal static void ValidateLimits(EvidenceManifestCompilationLimits limits)
    {
        if (limits.MaximumEntries <= 0 || limits.MaximumEntries > MaximumEntryCount ||
            limits.MaximumPathBytes <= 0 || limits.MaximumPathBytes > MaximumPathUtf8Bytes ||
            limits.MaximumEntryBytes <= 0 || limits.MaximumEntryBytes > MaximumEntryContentBytes ||
            limits.MaximumTotalBytes <= 0 || limits.MaximumTotalBytes > MaximumTotalContentBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }
    }

    private sealed record PreparedManifestSource(
        IEvidenceManifestEntrySource Source,
        string RelativePath,
        EvidenceManifestRecordKind Kind,
        EvidenceManifestContentKind ContentKind,
        long ContentLength);

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
        string exactPackageRoot) =>
        NormalizeProjectAssetsJson(bytes, exactRepositoryRoot, exactPackageRoot, MaximumEntryContentBytes);

    internal static byte[] NormalizeProjectAssetsJson(
        IReadOnlyList<byte> bytes,
        string exactRepositoryRoot,
        string exactPackageRoot,
        int maximumOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (maximumOutputBytes <= 0 || maximumOutputBytes > MaximumEntryContentBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        }
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
        using var output = new EvidenceBoundedWriteStream(maximumOutputBytes);
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            WriteNormalizedJson(writer, document.RootElement, repositoryRoot, packageRoot, jsonPath: "$");
        }
        if (output.Length > maximumOutputBytes)
        {
            throw new InvalidDataException("Normalized project assets JSON exceeded its output bound.");
        }
        return output.ToArrayExact();
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
                var normalizedProperties = new List<(JsonProperty Property, string Name)>();
                var exactNames = new HashSet<string>(StringComparer.Ordinal);
                var caseInsensitiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    var normalizedName = NormalizePropertyName(jsonPath, property.Name, repositoryRoot, packageRoot);
                    if (!exactNames.Add(normalizedName) || !caseInsensitiveNames.Add(normalizedName))
                    {
                        throw new InvalidDataException(
                            $"JSON property names collided after root normalization: {normalizedName}");
                    }
                    normalizedProperties.Add((property, normalizedName));
                }
                writer.WriteStartObject();
                foreach (var item in normalizedProperties.OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(item.Name);
                    WriteNormalizedPropertyValue(
                        writer, item.Property.Name, item.Property.Value, repositoryRoot, packageRoot,
                        jsonPath, jsonPath + "/" + EscapeJsonPathSegment(item.Name));
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
                var stringValue = element.GetString()!;
                if (ReferencesRoot(stringValue, repositoryRoot) || ReferencesRoot(stringValue, packageRoot))
                {
                    throw new InvalidDataException("An absolute root-bearing string appeared outside an admitted project.assets field.");
                }
                writer.WriteStringValue(stringValue);
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
        return segments.Length == 7 && segments[0] == "$" && segments[1] == "project" &&
            segments[2] == "restore" && segments[3] == "frameworks" &&
            segments[5] == "projectReferences";
    }

    private static string NormalizePropertyName(
        string objectPath,
        string propertyName,
        string repositoryRoot,
        string packageRoot)
    {
        if (objectPath == "$/packageFolders")
        {
            return NormalizeExactRootPropertyName(propertyName, packageRoot);
        }
        if (IsProjectReferencesDictionary(objectPath))
        {
            return NormalizeRepositoryValue(propertyName, repositoryRoot);
        }
        return propertyName;
    }

    private static bool IsProjectReferencesDictionary(string objectPath)
    {
        var segments = objectPath.Split('/');
        return segments.Length == 6 && segments[0] == "$" && segments[1] == "project" &&
            segments[2] == "restore" && segments[3] == "frameworks" &&
            segments[5] == "projectReferences";
    }

    private static string EscapeJsonPathSegment(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string NormalizeRepositoryValue(string value, string repositoryRoot)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(value);
        if (!IsOrdinaryAbsolutePath(trimmed)) throw new InvalidDataException("Named repository path was not an ordinary absolute path.");
        var separatorNormalized = NormalizeDirectorySeparators(trimmed);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(separatorNormalized));
        if (!string.Equals(separatorNormalized, full, StringComparison.OrdinalIgnoreCase))
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
        var separatorNormalized = NormalizeDirectorySeparators(trimmed);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(separatorNormalized));
        if (!string.Equals(separatorNormalized, full, StringComparison.OrdinalIgnoreCase) ||
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

    private static string NormalizeDirectorySeparators(string value) => value.Replace('/', '\\');

    private static bool ReferencesRoot(string value, string root)
    {
        if (!IsOrdinaryAbsolutePath(value)) return false;
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(NormalizeDirectorySeparators(value)));
        return string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StrictProjectAssetsNormalizer : IEvidenceProjectAssetsNormalizer
    {
        public byte[] Normalize(
            IReadOnlyList<byte> bytes,
            string exactRepositoryRoot,
            string exactPackageRoot,
            int maximumOutputBytes) =>
            NormalizeProjectAssetsJson(bytes, exactRepositoryRoot, exactPackageRoot, maximumOutputBytes);
    }

    private sealed class EvidenceBoundedWriteStream(int maximumBytes) : Stream
    {
        private readonly List<byte[]> _chunks = [];
        private long _length;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length - count) throw new ArgumentException("Write range exceeded its buffer.");
            RequireCapacity(count);
            if (count == 0) return;
            var chunk = new byte[count];
            Buffer.BlockCopy(buffer, offset, chunk, 0, count);
            _chunks.Add(chunk);
            _length = checked(_length + count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            RequireCapacity(buffer.Length);
            if (buffer.Length == 0) return;
            _chunks.Add(buffer.ToArray());
            _length = checked(_length + buffer.Length);
        }

        public override void WriteByte(byte value)
        {
            RequireCapacity(1);
            _chunks.Add([value]);
            _length = checked(_length + 1);
        }

        public byte[] ToArrayExact()
        {
            var result = new byte[checked((int)_length)];
            var offset = 0;
            foreach (var chunk in _chunks)
            {
                Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
                offset = checked(offset + chunk.Length);
            }
            return result;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private void RequireCapacity(int count)
        {
            if (count < 0 || _length > maximumBytes - count)
            {
                throw new InvalidDataException("Normalized project assets JSON exceeded its output bound.");
            }
        }
    }
}
