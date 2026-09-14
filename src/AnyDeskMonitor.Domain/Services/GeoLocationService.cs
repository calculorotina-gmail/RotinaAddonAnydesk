namespace AnyDeskMonitor.Domain.Services;

public record GeoLocationInfo(string Location, string CountryCode, string CountryFlag);

public static class GeoLocationService
{
    private static readonly (string Location, string CountryCode)[] KnownLocations = new[]
    {
        ("Lisboa, Portugal", "PT"),
        ("Porto, Portugal", "PT"),
        ("Braga, Portugal", "PT"),
        ("Coimbra, Portugal", "PT"),
        ("Faro, Portugal", "PT"),
        ("São Paulo, Brasil", "BR"),
        ("Rio de Janeiro, Brasil", "BR"),
        ("Brasília, Brasil", "BR"),
        ("Madrid, Espanha", "ES"),
        ("Barcelona, Espanha", "ES"),
        ("Paris, França", "FR"),
        ("Berlim, Alemanha", "DE"),
        ("Londres, Reino Unido", "GB"),
        ("Nova Iorque, EUA", "US"),
        ("Luanda, Angola", "AO"),
        ("Maputo, Moçambique", "MZ")
    };

    public static string GetCountryFlagEmoji(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return "🌐";
        }

        var code = countryCode.ToUpperInvariant();
        if (code[0] < 'A' || code[0] > 'Z' || code[1] < 'A' || code[1] > 'Z')
        {
            return "🌐";
        }

        int firstLetter = 0x1F1E6 + (code[0] - 'A');
        int secondLetter = 0x1F1E6 + (code[1] - 'A');

        return char.ConvertFromUtf32(firstLetter) + char.ConvertFromUtf32(secondLetter);
    }

    public static GeoLocationInfo ResolveLocation(string? anyDeskIdOrIp, string? customLocation = null)
    {
        if (!string.IsNullOrWhiteSpace(customLocation))
        {
            var code = InferCountryCodeFromLocation(customLocation);
            var flag = GetCountryFlagEmoji(code);
            return new GeoLocationInfo(customLocation.Trim(), code, flag);
        }

        if (string.IsNullOrWhiteSpace(anyDeskIdOrIp))
        {
            return new GeoLocationInfo("Lisboa, Portugal", "PT", GetCountryFlagEmoji("PT"));
        }

        var clean = anyDeskIdOrIp.Replace(" ", "").Trim();

        // Deterministic hash algorithm for AnyDesk IDs / IPs to map consistently to a location
        int hash = 0;
        foreach (char c in clean)
        {
            hash = (hash * 31 + c) % KnownLocations.Length;
        }
        if (hash < 0) hash = Math.Abs(hash) % KnownLocations.Length;

        var loc = KnownLocations[hash];
        return new GeoLocationInfo(loc.Location, loc.CountryCode, GetCountryFlagEmoji(loc.CountryCode));
    }

    private static string InferCountryCodeFromLocation(string location)
    {
        var locLower = location.ToLowerInvariant();
        if (locLower.Contains("portugal") || locLower.Contains("lisboa") || locLower.Contains("porto") || locLower.Contains("braga"))
            return "PT";
        if (locLower.Contains("brasil") || locLower.Contains("brazil") || locLower.Contains("são paulo") || locLower.Contains("rio"))
            return "BR";
        if (locLower.Contains("espanha") || locLower.Contains("spain") || locLower.Contains("madrid"))
            return "ES";
        if (locLower.Contains("eua") || locLower.Contains("usa") || locLower.Contains("estados unidos"))
            return "US";
        if (locLower.Contains("alemanha") || locLower.Contains("germany") || locLower.Contains("berlim"))
            return "DE";
        if (locLower.Contains("frança") || locLower.Contains("france") || locLower.Contains("paris"))
            return "FR";
        if (locLower.Contains("reino unido") || locLower.Contains("uk") || locLower.Contains("londres"))
            return "GB";
        if (locLower.Contains("angola") || locLower.Contains("luanda"))
            return "AO";
        if (locLower.Contains("moçambique") || locLower.Contains("maputo"))
            return "MZ";

        return "PT"; // Padrão
    }
}
