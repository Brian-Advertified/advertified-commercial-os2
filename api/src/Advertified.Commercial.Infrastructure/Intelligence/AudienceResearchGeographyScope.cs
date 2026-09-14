namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class AudienceResearchGeographyScope
{
    private static readonly Dictionary<string, string> ProvinceAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GP"] = "Gauteng",
        ["WC"] = "Western Cape",
        ["EC"] = "Eastern Cape",
        ["KZN"] = "KwaZulu-Natal",
        ["FS"] = "Free State",
        ["NW"] = "North West",
        ["NC"] = "Northern Cape",
        ["MP"] = "Mpumalanga",
        ["LP"] = "Limpopo",
        ["Gauteng"] = "Gauteng",
        ["Western Cape"] = "Western Cape",
        ["Eastern Cape"] = "Eastern Cape",
        ["KwaZulu-Natal"] = "KwaZulu-Natal",
        ["KwaZulu Natal"] = "KwaZulu-Natal",
        ["Free State"] = "Free State",
        ["North West"] = "North West",
        ["Northern Cape"] = "Northern Cape",
        ["Mpumalanga"] = "Mpumalanga",
        ["Limpopo"] = "Limpopo",
    };

    private static readonly Dictionary<string, string> PlaceToProvince = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Johannesburg"] = "Gauteng",
        ["Joburg"] = "Gauteng",
        ["Jo'burg"] = "Gauteng",
        ["Sandton"] = "Gauteng",
        ["Soweto"] = "Gauteng",
        ["Midrand"] = "Gauteng",
        ["Randburg"] = "Gauteng",
        ["Roodepoort"] = "Gauteng",
        ["Alexandra"] = "Gauteng",
        ["Pretoria"] = "Gauteng",
        ["Tshwane"] = "Gauteng",
        ["Centurion"] = "Gauteng",
        ["Mamelodi"] = "Gauteng",
        ["Soshanguve"] = "Gauteng",
        ["Ekurhuleni"] = "Gauteng",
        ["Tembisa"] = "Gauteng",
        ["Kempton Park"] = "Gauteng",
        ["Germiston"] = "Gauteng",
        ["Alberton"] = "Gauteng",
        ["Boksburg"] = "Gauteng",
        ["Benoni"] = "Gauteng",
        ["Springs"] = "Gauteng",
        ["Katlehong"] = "Gauteng",
        ["Cape Town"] = "Western Cape",
        ["Khayelitsha"] = "Western Cape",
        ["Langa"] = "Western Cape",
        ["Gugulethu"] = "Western Cape",
        ["Mitchells Plain"] = "Western Cape",
        ["Bellville"] = "Western Cape",
        ["Stellenbosch"] = "Western Cape",
        ["Durban"] = "KwaZulu-Natal",
        ["eThekwini"] = "KwaZulu-Natal",
        ["Umhlanga"] = "KwaZulu-Natal",
        ["Umlazi"] = "KwaZulu-Natal",
        ["KwaMashu"] = "KwaZulu-Natal",
        ["Inanda"] = "KwaZulu-Natal",
        ["Ballito"] = "KwaZulu-Natal",
        ["Pietermaritzburg"] = "KwaZulu-Natal",
        ["Gqeberha"] = "Eastern Cape",
        ["Port Elizabeth"] = "Eastern Cape",
        ["East London"] = "Eastern Cape",
        ["Bloemfontein"] = "Free State",
        ["Polokwane"] = "Limpopo",
        ["Mbombela"] = "Mpumalanga",
        ["Nelspruit"] = "Mpumalanga",
        ["Rustenburg"] = "North West",
        ["Mahikeng"] = "North West",
        ["Mafikeng"] = "North West",
        ["Kimberley"] = "Northern Cape",
    };

    internal static IReadOnlyList<string> Expand(IReadOnlyList<string> geographies)
    {
        var expanded = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in geographies)
        {
            var value = raw.Trim();
            if (value.Length == 0) continue;
            Add(value);
            if (ProvinceAliases.TryGetValue(value, out var province)) Add(province);
            if (PlaceToProvince.TryGetValue(value, out province)) Add(province);
        }
        return expanded;

        void Add(string value)
        {
            if (seen.Add(value)) expanded.Add(value);
        }
    }
}
