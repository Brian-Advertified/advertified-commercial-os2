using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryKnownSourcePolicyTests
{
    [Theory]
    [MemberData(nameof(KnownSources))]
    public void EveryCorpusSourceReceivesProductionIdentity(
        string fileName,
        string expectedSupplier,
        string expectedChannel)
    {
        var sourceHash = new string('a', 64);
        var row = new InventoryExtractedRow(
            1,
            "source:row=1",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "Grounded physical offer",
                ["rate"] = "R100",
                ["currency"] = "ZAR",
            });
        var extraction = InventoryExtractionContract.Create(
            "fixture",
            "fixture-v1",
            InventoryExtractionOptions.CurrentSchemaVersion,
            sourceHash,
            "{}",
            [row]);
        var request = new InventoryExtractionRequest(
            fileName,
            MediaType(fileName),
            DocumentClass(fileName),
            sourceHash,
            [1]);

        var governed = InventoryKnownSourcePolicy.Apply(request, extraction);
        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(governed.Rows),
            sourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal(expectedSupplier, candidate.SupplierName);
        Assert.Equal(expectedChannel, candidate.Values.Channel);
        Assert.False(string.IsNullOrWhiteSpace(candidate.Values.ProductType));
        Assert.Matches("^ADV-[A-F0-9]{16}$", candidate.Values.ProductCode!);
    }

    public static TheoryData<string, string, string> KnownSources => new()
    {
        { "Algoa FM - Algoa Club Package - Plan A - Generic & Sponsorship -2026.pdf", "Algoa FM", MasterDataCodes.Channels.Radio },
        { "Algoa FM - Algoa Club Package - Plan A - Generic Only -2026.pdf", "Algoa FM", MasterDataCodes.Channels.Radio },
        { "Algoa FM - Club Package - Generic & Pre-Rolls - 6 Months - 2026.pdf", "Algoa FM", MasterDataCodes.Channels.Radio },
        { "Arena-Business-Day-Rate-Sheet-2026-Repro.pdf", "Arena Holdings", MasterDataCodes.Channels.Print },
        { "Arena-Daily-Dispatch-2026-Rate-Sheet1.pdf", "Arena Holdings", MasterDataCodes.Channels.Print },
        { "Arena-Sowetan-2026-Rate-Sheet_Repro77.pdf", "Arena Holdings", MasterDataCodes.Channels.Print },
        { "Arena-Sunday-Times-Rate-Sheet-2026-Repro.pdf", "Arena Holdings", MasterDataCodes.Channels.Print },
        { "Arena-The-Herald-2026-Rate-Sheet1.pdf", "Arena Holdings", MasterDataCodes.Channels.Print },
        { "BlackSpace VSBLT_OOH Sites Q3_ 2025.pdf", "BlackSpace", MasterDataCodes.Channels.Ooh },
        { "Business Day TV Rate Card - 2025.pdf", "Arena Holdings", MasterDataCodes.Channels.Tv },
        { "Digital Rates & Packages F27_FINAL.pdf", "DStv Media Sales", MasterDataCodes.Channels.Digital },
        { "Digital Screens Concept - Soweto Screens 11 April 2026 (1).pdf", "DStv Media Sales", MasterDataCodes.Channels.Ooh },
        { "Direct Kaya Packages (Discounted) .pdf", "Kaya 959", MasterDataCodes.Channels.Radio },
        { "DMS Digital Rate Card .xlsx", "DStv Media Sales", MasterDataCodes.Channels.Digital },
        { "eleven8_inventory.xlsx", "Eleven8", MasterDataCodes.Channels.Digital },
        { "eMedia Rate Card June 2026.pdf", "eMedia", MasterDataCodes.Channels.Tv },
        { "Ignition TV Rate Card - 2025.pdf", "Ignition TV", MasterDataCodes.Channels.Tv },
        { "Insight Outdoor ZA - Publisher Media Kit - 2025.pptx", "Insight Outdoor ZA", MasterDataCodes.Channels.Ooh },
        { "JAC Rate Card_2026.pdf", "Jacaranda FM", MasterDataCodes.Channels.Radio },
        { "JCDecaux ZA - Media Kit - 2025.pdf", "JCDecaux ZA", MasterDataCodes.Channels.Ooh },
        { "Jit Tv Digital Screens Concept.pdf", "Jit TV", MasterDataCodes.Channels.Tv },
        { "Jozi FM - Club Package - Generic & Pre-Rolls - 6 Months - 2026.pdf", "Jozi FM", MasterDataCodes.Channels.Radio },
        { "Jozi FM - Plan A - Generic & Sponsorship - 2026.pdf", "Jozi FM", MasterDataCodes.Channels.Radio },
        { "Jozi FM - Plan A - Generic Only - 2026.pdf", "Jozi FM", MasterDataCodes.Channels.Radio },
        { "Kena Outdoor Digital Inventory - Sept 2025.pdf", "Kena Outdoor", MasterDataCodes.Channels.Ooh },
        { "Kena Outdoor Site Inventory -African Bank September Avails.pdf", "Kena Outdoor", MasterDataCodes.Channels.Ooh },
        { "MAMG rate card 2023-2024.pdf", "MAMG", MasterDataCodes.Channels.Digital },
        { "Media Deck 2026 (1).pptx", "Volt Africa", MasterDataCodes.Channels.Digital },
        { "Primedia Broadcasting_Rate Card_FY27.pdf", "Primedia Broadcasting", MasterDataCodes.Channels.Radio },
        { "Primedia Outdoor ZA - Programmatic Digital - 2025.pdf", "Primedia Outdoor ZA", MasterDataCodes.Channels.Ooh },
        { "Relativ Media ZA - Media Kit - 2025.pdf", "Relativ Media ZA", MasterDataCodes.Channels.Ooh },
        { "Reveel - ZA - Publisher Media Kit.pptx", "Reveel", MasterDataCodes.Channels.Ooh },
        { "RSD Rate Cards - Gauteng - 2025.pptx", "Roadside Digital", MasterDataCodes.Channels.Ooh },
        { "RSD Rate Cards - Western Cape - 2025.pptx", "Roadside Digital", MasterDataCodes.Channels.Ooh },
        { "SABC May 2026 TV Rates (1).pdf", "SABC", MasterDataCodes.Channels.Tv },
        { "SABC Radio Rates F2025-2026 (3) (1) (1) - Copy.pdf", "SABC", MasterDataCodes.Channels.Radio },
        { "SB Outdoor - ZA - Publisher Media Kit.PPTX", "SB Outdoor", MasterDataCodes.Channels.Ooh },
        { "Smile 90.4FM - Impact Plus package - 2026.pdf", "Smile 90.4FM", MasterDataCodes.Channels.Radio },
        { "Summit OOH Media - Digital Billboard Network - 2025.pptx", "Summit OOH Media", MasterDataCodes.Channels.Ooh },
        { "Summit OOH Media - Main Market Screens - 2025.pptx", "Summit OOH Media", MasterDataCodes.Channels.Ooh },
        { "The Home Channel Rate Card - 2025.pdf", "The Home Channel", MasterDataCodes.Channels.Tv },
        { "Virgin Active ZA - Media Kit - 2025.pdf", "Virgin Active", MasterDataCodes.Channels.Ooh },
        { "Y PACKAGES ONE PAGER.pdf", "YFM", MasterDataCodes.Channels.Radio },
    };

    private static string DocumentClass(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => MasterDataCodes.DocumentClasses.Pdf,
            ".pptx" => MasterDataCodes.DocumentClasses.Pptx,
            ".xlsx" => MasterDataCodes.DocumentClasses.Xlsx,
            _ => throw new InvalidOperationException(),
        };

    private static string MediaType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
}
