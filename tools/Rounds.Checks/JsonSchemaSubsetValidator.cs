using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rounds.Checks;

internal sealed class JsonSchemaSubsetValidator
{
    private static readonly HashSet<string> SupportedKeywords =
    [
        "$defs",
        "$id",
        "$ref",
        "$schema",
        "additionalProperties",
        "const",
        "enum",
        "exclusiveMinimum",
        "format",
        "items",
        "minItems",
        "minLength",
        "minProperties",
        "minimum",
        "pattern",
        "properties",
        "required",
        "title",
        "type",
        "uniqueItems",
    ];

    private readonly JsonElement _rootSchema;
    private readonly List<string> _failures = [];

    private JsonSchemaSubsetValidator(JsonElement rootSchema)
    {
        _rootSchema = rootSchema;
    }

    public static IReadOnlyList<string> Validate(JsonElement schema, JsonElement instance)
    {
        var validator = new JsonSchemaSubsetValidator(schema);
        validator.CheckSchemaVocabulary(schema, "#");
        validator.Evaluate(schema, instance, "$", new HashSet<string>(StringComparer.Ordinal));
        return validator._failures;
    }

    private void CheckSchemaVocabulary(JsonElement schema, string schemaPath)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            _failures.Add($"{schemaPath}: schema must be an object or boolean.");
            return;
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(keyword.Name))
            {
                _failures.Add($"{schemaPath}: unsupported schema keyword `{keyword.Name}`.");
                continue;
            }

            if (keyword.Name is "properties" or "$defs")
            {
                if (keyword.Value.ValueKind != JsonValueKind.Object)
                {
                    _failures.Add($"{schemaPath}/{keyword.Name}: expected an object.");
                    continue;
                }

                foreach (var child in keyword.Value.EnumerateObject())
                {
                    CheckSchemaVocabulary(child.Value, $"{schemaPath}/{keyword.Name}/{Escape(child.Name)}");
                }
            }
            else if ((keyword.Name is "items" or "additionalProperties") && keyword.Value.ValueKind == JsonValueKind.Object)
            {
                CheckSchemaVocabulary(keyword.Value, $"{schemaPath}/{keyword.Name}");
            }
        }
    }

    private void Evaluate(JsonElement schema, JsonElement instance, string path, HashSet<string> references)
    {
        if (schema.ValueKind == JsonValueKind.False)
        {
            _failures.Add($"{path}: rejected by the false schema.");
            return;
        }

        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            var reference = referenceElement.GetString() ?? string.Empty;
            if (!references.Add(reference))
            {
                _failures.Add($"{path}: cyclic schema reference `{reference}`.");
                return;
            }

            if (!TryResolveReference(reference, out var target))
            {
                _failures.Add($"{path}: unresolved schema reference `{reference}`.");
                return;
            }

            Evaluate(target, instance, path, references);
            references.Remove(reference);
        }

        if (schema.TryGetProperty("type", out var type) && !MatchesType(type.GetString(), instance))
        {
            _failures.Add($"{path}: expected {type.GetString()}, found {instance.ValueKind}.");
            return;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonEquals(constant, instance))
        {
            _failures.Add($"{path}: value does not equal the required constant.");
        }

        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(item => JsonEquals(item, instance)))
        {
            _failures.Add($"{path}: value is not in the allowed set.");
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            EvaluateObject(schema, instance, path, references);
        }
        else if (instance.ValueKind == JsonValueKind.Array)
        {
            EvaluateArray(schema, instance, path, references);
        }
        else if (instance.ValueKind == JsonValueKind.String)
        {
            EvaluateString(schema, instance, path);
        }
        else if (instance.ValueKind == JsonValueKind.Number)
        {
            EvaluateNumber(schema, instance, path);
        }
    }

    private void EvaluateObject(JsonElement schema, JsonElement instance, string path, HashSet<string> references)
    {
        var properties = schema.TryGetProperty("properties", out var declaredProperties)
            ? declaredProperties
            : default;
        var propertyNames = instance.EnumerateObject().Select(property => property.Name).ToArray();

        if (schema.TryGetProperty("minProperties", out var minimumProperties) && propertyNames.Length < minimumProperties.GetInt32())
        {
            _failures.Add($"{path}: expected at least {minimumProperties.GetInt32()} properties.");
        }

        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var requiredName in required.EnumerateArray().Select(item => item.GetString()!))
            {
                if (!instance.TryGetProperty(requiredName, out _))
                {
                    _failures.Add($"{path}: missing required property `{requiredName}`.");
                }
            }
        }

        foreach (var property in instance.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                Evaluate(propertySchema, property.Value, $"{path}.{property.Name}", new HashSet<string>(references, StringComparer.Ordinal));
                continue;
            }

            if (!schema.TryGetProperty("additionalProperties", out var additional))
            {
                continue;
            }

            if (additional.ValueKind == JsonValueKind.False)
            {
                _failures.Add($"{path}: undeclared property `{property.Name}` is not allowed.");
            }
            else if (additional.ValueKind == JsonValueKind.Object)
            {
                Evaluate(additional, property.Value, $"{path}.{property.Name}", new HashSet<string>(references, StringComparer.Ordinal));
            }
        }
    }

    private void EvaluateArray(JsonElement schema, JsonElement instance, string path, HashSet<string> references)
    {
        var items = instance.EnumerateArray().ToArray();
        if (schema.TryGetProperty("minItems", out var minimumItems) && items.Length < minimumItems.GetInt32())
        {
            _failures.Add($"{path}: expected at least {minimumItems.GetInt32()} items.");
        }

        if (schema.TryGetProperty("uniqueItems", out var uniqueItems) && uniqueItems.GetBoolean())
        {
            for (var left = 0; left < items.Length; left++)
            {
                for (var right = left + 1; right < items.Length; right++)
                {
                    if (JsonEquals(items[left], items[right]))
                    {
                        _failures.Add($"{path}: items {left} and {right} are duplicates.");
                    }
                }
            }
        }

        if (!schema.TryGetProperty("items", out var itemSchema))
        {
            return;
        }

        for (var index = 0; index < items.Length; index++)
        {
            Evaluate(itemSchema, items[index], $"{path}[{index}]", new HashSet<string>(references, StringComparer.Ordinal));
        }
    }

    private void EvaluateString(JsonElement schema, JsonElement instance, string path)
    {
        var value = instance.GetString() ?? string.Empty;
        if (schema.TryGetProperty("minLength", out var minimumLength) && value.Length < minimumLength.GetInt32())
        {
            _failures.Add($"{path}: string is shorter than {minimumLength.GetInt32()} characters.");
        }

        if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            _failures.Add($"{path}: string does not match `{pattern.GetString()}`.");
        }

        if (schema.TryGetProperty("format", out var format) && format.GetString() == "date" &&
            !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            _failures.Add($"{path}: `{value}` is not an ISO calendar date.");
        }
    }

    private void EvaluateNumber(JsonElement schema, JsonElement instance, string path)
    {
        var value = instance.GetDouble();
        if (schema.TryGetProperty("minimum", out var minimum) && value < minimum.GetDouble())
        {
            _failures.Add($"{path}: {value} is less than {minimum.GetDouble()}.");
        }

        if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimum) && value <= exclusiveMinimum.GetDouble())
        {
            _failures.Add($"{path}: {value} must be greater than {exclusiveMinimum.GetDouble()}.");
        }
    }

    private bool TryResolveReference(string reference, out JsonElement target)
    {
        target = _rootSchema;
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var token in reference[2..].Split('/').Select(Unescape))
        {
            if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(token, out target))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesType(string? type, JsonElement instance) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && IsInteger(instance),
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        null => true,
        _ => false,
    };

    private static bool IsInteger(JsonElement instance)
    {
        if (instance.TryGetDecimal(out var decimalValue))
        {
            return decimal.Truncate(decimalValue) == decimalValue;
        }

        var doubleValue = instance.GetDouble();
        return double.IsFinite(doubleValue) && Math.Truncate(doubleValue) == doubleValue;
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(left, right),
            JsonValueKind.Array => left.EnumerateArray().SequenceEqual(right.EnumerateArray(), JsonElementComparer.Instance),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetDecimal() == right.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            _ => false,
        };
    }

    private static bool ObjectEquals(JsonElement left, JsonElement right)
    {
        var leftProperties = left.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        var rightProperties = right.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        return leftProperties.Length == rightProperties.Length && leftProperties.Zip(rightProperties).All(pair =>
            pair.First.Name == pair.Second.Name && JsonEquals(pair.First.Value, pair.Second.Value));
    }

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string Unescape(string value) => value.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private sealed class JsonElementComparer : IEqualityComparer<JsonElement>
    {
        public static readonly JsonElementComparer Instance = new();

        public bool Equals(JsonElement left, JsonElement right) => JsonEquals(left, right);

        public int GetHashCode(JsonElement value) => value.GetRawText().GetHashCode(StringComparison.Ordinal);
    }
}
