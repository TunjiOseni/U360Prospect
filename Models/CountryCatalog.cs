namespace U360Prospect.Models;

public sealed record CountryOption(string Code, string DisplayName);

public static class CountryCatalog
{
    public static IReadOnlyList<CountryOption> All { get; } =
        new CountryOption[]
        {
            new("GN", "UBA Guinea"),
            new("MZ", "UBA Mozambique"),
            new("CD", "UBA Congo DRC"),
            new("TZ", "UBA Tanzania"),
            new("NG", "UBA Nigeria Plc"),
            new("CI", "UBA Cote D'Ivoire"),
            new("GB", "UBA UK"),
            new("GA", "UBA Gabon"),
            new("BJ", "UBA Benin"),
            new("BF", "UBA Burkina Faso"),
            new("SN", "UBA Senegal"),
            new("ZM", "UBA Zambia"),
            new("US", "UBA USA"),
            new("CM", "UBA Cameroon"),
            new("UG", "UBA Uganda"),
            new("TD", "UBA Chad"),
            new("GH", "UBA Ghana"),
            new("KE", "UBA Kenya"),
            new("LR", "UBA Liberia"),
            new("SL", "UBA Sierra Leone"),
            new("CG", "UBA Congo Brazzaville"),
            new("ML", "UBA Mali")
        };

    private static readonly Dictionary<string, string> CodeLookup =
        BuildLookup();

    public static bool TryNormalize(
        string? value,
        out string code)
    {
        var key = NormalizeKey(value);

        if (CodeLookup.TryGetValue(key, out var result))
        {
            code = result;
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static Dictionary<string, string> BuildLookup()
    {
        var lookup =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var country in All)
        {
            Add(country.Code, country.Code);
            Add(country.DisplayName, country.Code);

            var name = country.DisplayName;

            if (name.StartsWith(
                    "UBA ",
                    StringComparison.OrdinalIgnoreCase))
            {
                name = name[4..];
            }

            name = name.Replace(
                " Plc",
                "",
                StringComparison.OrdinalIgnoreCase).Trim();

            Add(name, country.Code);
        }

        Add("Nigeria", "NG");
        Add("Mozambique", "MZ");
        Add("Guinea", "GN");
        Add("Tanzania", "TZ");
        Add("Cote d'Ivoire", "CI");
        Add("Côte d'Ivoire", "CI");
        Add("Ivory Coast", "CI");
        Add("United Kingdom", "GB");
        Add("UK", "GB");
        Add("United States", "US");
        Add("United States of America", "US");
        Add("USA", "US");
        Add("Democratic Republic of the Congo", "CD");
        Add("DR Congo", "CD");
        Add("Republic of the Congo", "CG");
        Add("Congo-Brazzaville", "CG");

        return lookup;

        void Add(string input, string countryCode)
        {
            lookup[NormalizeKey(input)] = countryCode;
        }
    }

    private static string NormalizeKey(string? value)
    {
        return new string(
            (value ?? string.Empty)
                .Trim()
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}
