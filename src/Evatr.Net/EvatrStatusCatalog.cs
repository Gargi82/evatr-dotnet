using System.Text.Json;
using Evatr.Net.Models;

namespace Evatr.Net;

/// <summary>
/// Parses the eVatR status catalog. The live API returns a top-level array of
/// <c>{status, kategorie, httpcode, feld, meldung}</c> objects (<c>httpcode</c> and <c>feld</c>
/// are absent on some entries). The parser stays tolerant of a wrapping object and alternative
/// field names in case the schema drifts.
/// </summary>
public static class EvatrStatusCatalog
{
    public static IReadOnlyList<EvatrStatus> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var array = FindArray(doc.RootElement) ?? throw new FormatException("Status catalog JSON contains no array of entries.");

        var result = new List<EvatrStatus>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var code = GetString(item, "status", "code");
            if (string.IsNullOrWhiteSpace(code)) continue;
            result.Add(new EvatrStatus(
                code,
                GetString(item, "kategorie", "category"),
                GetInt(item, "httpcode", "httpStatus"),
                GetString(item, "meldung", "text", "message"),
                GetString(item, "feld", "field")));
        }

        return result;
    }

    private static JsonElement? FindArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array) return prop.Value;
            }
        }

        return null;
    }

    private static JsonElement? Find(JsonElement obj, string[] names)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (names.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)) return prop.Value;
        }

        return null;
    }

    private static string? GetString(JsonElement obj, params string[] names) =>
        Find(obj, names) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static int? GetInt(JsonElement obj, params string[] names) => Find(obj, names) switch
    {
        { ValueKind: JsonValueKind.Number } v when v.TryGetInt32(out var i) => i,
        { ValueKind: JsonValueKind.String } v when int.TryParse(v.GetString(), out var i) => i,
        _ => null,
    };
}
