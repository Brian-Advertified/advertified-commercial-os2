using System.Text;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private sealed record FileFixture(
        string DocumentClass,
        string FileName,
        string MediaType,
        byte[] Content);

    private static FileFixture CsvFixture(
        string code = "OOH-001",
        string name = "Bree Street Gantry") => new(
        "CSV", "held-out-sites.csv", "text/csv", Encoding.UTF8.GetBytes(
            "product_code,name,channel,geography,latitude,longitude,rate_type,currency," +
            "rate_minor,availability,spoken_languages,life_stages,lsm_sem," +
            "audience_taxonomy,audience_taxonomy_version,audience_universe," +
            "audience_source,audience_period,audience_methodology,audience_limitations," +
            "reach,reach_unit,footfall,footfall_unit\n" +
            $"{code},{name},OOH,Johannesburg," +
            "-26.2041,28.0473,MONTH_RATE,ZAR,125000,UNKNOWN,English:80%," +
            "Business decision makers:60%,SEM 8-10:70%,TGI SEM,2026," +
            "Johannesburg adults,Fixture audience study,2026 Q2," +
            "Weighted aggregate survey,Test fixture only,125000,PEOPLE,42000,PEOPLE\n"));
}
