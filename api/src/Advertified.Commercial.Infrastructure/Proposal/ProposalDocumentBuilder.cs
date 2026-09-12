using System.Globalization;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalDocumentBuilder
{
    private static readonly Color Neutral = Color.Parse("#E8ECF2");
    private static readonly Color Muted = Color.Parse("#5E6675");

    internal static Document Build(
        ProposalVersionView proposal,
        byte[]? agencyLogo,
        byte[]? clientLogo,
        ProposalCampaignContextView? campaignContext)
    {
        var document = new Document();
        document.Info.Title = proposal.Title;
        document.Info.Author = proposal.Branding.AgencyName;
        document.Info.Subject = $"Proposal for {proposal.Branding.ClientBrandName}";
        ConfigureStyles(document, BrandColour(proposal.Branding.PrimaryColour));
        var section = document.AddSection();
        ConfigurePage(section);
        AddHeaderAndFooter(section, proposal.Branding);
        AddCover(section, proposal, agencyLogo, clientLogo);
        section.AddPageBreak();
        AddCampaignDirection(section, campaignContext);
        AddOptions(section, proposal);
        AddTermsAndNextStep(section, proposal);
        return document;
    }

    private static void ConfigureStyles(Document document, Color brand)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = ProposalFontResolver.FamilyName;
        normal.Font.Size = 9;
        normal.Font.Color = Color.Parse("#18202D");
        normal.ParagraphFormat.SpaceAfter = 5;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.AtLeast;
        normal.ParagraphFormat.LineSpacing = 12;

        var heading1 = document.Styles[StyleNames.Heading1]!;
        heading1.Font.Name = ProposalFontResolver.FamilyName;
        heading1.Font.Size = 17;
        heading1.Font.Bold = true;
        heading1.Font.Color = brand;
        heading1.ParagraphFormat.SpaceBefore = 10;
        heading1.ParagraphFormat.SpaceAfter = 8;
        heading1.ParagraphFormat.KeepWithNext = true;

        var heading2 = document.Styles[StyleNames.Heading2]!;
        heading2.Font.Name = ProposalFontResolver.FamilyName;
        heading2.Font.Size = 12;
        heading2.Font.Bold = true;
        heading2.Font.Color = Color.Parse("#18202D");
        heading2.ParagraphFormat.SpaceBefore = 8;
        heading2.ParagraphFormat.SpaceAfter = 5;
        heading2.ParagraphFormat.KeepWithNext = true;
    }

    private static void ConfigurePage(Section section)
    {
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.7);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.7);
    }

    private static void AddHeaderAndFooter(Section section, ProposalBrandingView branding)
    {
        var header = section.Headers.Primary.AddParagraph(
            $"{branding.AgencyName}  ·  PROPOSAL FOR {branding.ClientBrandName}");
        header.Format.Alignment = ParagraphAlignment.Right;
        header.Format.Font.Name = ProposalFontResolver.FamilyName;
        header.Format.Font.Size = 7;
        header.Format.Font.Color = Muted;
        header.Format.Borders.Bottom.Color = Neutral;
        header.Format.Borders.Bottom.Width = 0.5;
        header.Format.SpaceAfter = 4;

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Name = ProposalFontResolver.FamilyName;
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Muted;
        footer.Format.Borders.Top.Color = Neutral;
        footer.Format.Borders.Top.Width = 0.5;
        footer.Format.SpaceBefore = 4;
        footer.AddText($"advertified.co.za  |  {branding.AgencyName}  |  Confidential proposal  |  Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
    }

    private static void AddCover(
        Section section,
        ProposalVersionView proposal,
        byte[]? agencyLogo,
        byte[]? clientLogo)
    {
        var brand = BrandColour(proposal.Branding.PrimaryColour);
        var identity = section.AddTable();
        identity.AddColumn(Unit.FromCentimeter(8.9));
        identity.AddColumn(Unit.FromCentimeter(8.9));
        var row = identity.AddRow();
        row.Cells[0].VerticalAlignment = VerticalAlignment.Center;
        row.Cells[1].VerticalAlignment = VerticalAlignment.Center;
        row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
        AddLogoOrName(row.Cells[0], agencyLogo, proposal.Branding.AgencyName, false);
        AddLogoOrName(row.Cells[1], clientLogo, proposal.Branding.ClientBrandName, true);

        var kicker = section.AddParagraph(IsOohProposal(proposal) ? "OOH CAMPAIGN PROPOSAL" : "CAMPAIGN PROPOSAL");
        kicker.Format.Font.Name = ProposalFontResolver.FamilyName;
        kicker.Format.Font.Size = 9;
        kicker.Format.Font.Bold = true;
        kicker.Format.Font.Color = brand;
        kicker.Format.SpaceBefore = 42;
        kicker.Format.SpaceAfter = 9;

        var title = section.AddParagraph(proposal.Title);
        title.Format.Font.Name = ProposalFontResolver.FamilyName;
        title.Format.Font.Size = 28;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.Parse("#111827");
        title.Format.SpaceAfter = 14;

        var summary = section.AddParagraph(proposal.ExecutiveSummary);
        summary.Format.Font.Size = 12;
        summary.Format.Font.Color = Color.Parse("#374151");
        summary.Format.SpaceAfter = 18;

        AddKeyValue(section, "Prepared for", proposal.Branding.ClientBrandName, brand);
        AddKeyValue(section, "Prepared by", proposal.Branding.AgencyName, brand);
        AddKeyValue(section, "Valid until", proposal.ExpiryAtUtc.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), brand);
        var notice = section.AddParagraph(BrandingNotice(proposal.Branding));
        notice.Format.Font.Size = 8;
        notice.Format.Font.Color = Muted;
        notice.Format.SpaceBefore = 18;
    }

    private static void AddCampaignDirection(Section section, ProposalCampaignContextView? context)
    {
        if (context is null) return;
        section.AddParagraph("Campaign direction", StyleNames.Heading1);
        AddLabelledParagraph(section, "Business challenge", context.BusinessProblem);
        AddLabelledParagraph(section, "Objective", context.Objective);
        AddListField(section, "Target audience", context.TargetAudiences);
        if (!string.IsNullOrWhiteSpace(context.TargetingRationale))
            AddLabelledParagraph(section, "Audience rationale", context.TargetingRationale);
        if (!string.IsNullOrWhiteSpace(context.PositioningStatement))
            AddLabelledParagraph(section, "Positioning", context.PositioningStatement);
        AddListField(section, "Priority geography", context.Geographies);
        AddListField(section, "Approved success measures", context.SuccessMeasures);
        if (context.InventoryOptionsEvaluated > 0) AddPlanningProof(section, context);
        if (!context.AudienceDirectionConsistent)
        {
            AddCallout(section,
                "Audience direction differs across approved plan options. Compare the retained outcome and media composition for each option.");
        }
    }

    private static void AddOptions(Section section, ProposalVersionView proposal)
    {
        section.AddParagraph("Investment options", StyleNames.Heading1);
        foreach (var option in proposal.Options.OrderBy(item => item.DisplayOrder))
        {
            section.AddParagraph(option.Label, StyleNames.Heading2);
            AddOptionSummary(section, option);
            AddMediaMix(section, option);
            AddInventorySchedule(section, option, IsOohProposal(proposal));
            AddOptionLimitations(section, option);
        }
    }

    private static void AddOptionSummary(Section section, ProposalOptionView option)
    {
        var table = section.AddTable();
        table.Borders.Color = Neutral;
        table.Borders.Width = 0.5;
        table.AddColumn(Unit.FromCentimeter(4.2));
        table.AddColumn(Unit.FromCentimeter(13.6));
        AddSummaryRow(table, "Client investment", ProposalMoneyFormatter.Format(option.BudgetMinor, option.Currency));
        AddSummaryRow(table, "Outcome", option.Outcome);
        AddSummaryRow(table, "Channels", string.Join(", ", option.Channels.Select(ProposalMediaLabels.Channel)));
        AddSummaryRow(table, "Running period", Periods(option.RunningPeriods));
    }

    private static void AddMediaMix(Section section, ProposalOptionView option)
    {
        if (option.Inventory.Count == 0) return;
        var byChannel = option.Inventory
            .GroupBy(item => item.Channel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var heading = section.AddParagraph("Media mix by selected placements");
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = 7;
        var table = section.AddTable();
        table.Borders.Color = Neutral;
        table.Borders.Width = 0.5;
        table.AddColumn(Unit.FromCentimeter(8.9));
        table.AddColumn(Unit.FromCentimeter(8.9));
        var header = table.AddRow();
        header.HeadingFormat = true;
        SetHeader(header, "Channel", "Selected placements");
        foreach (var group in byChannel)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(ProposalMediaLabels.Channel(group.Key));
            row.Cells[1].AddParagraph(group.Count().ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddInventorySchedule(Section section, ProposalOptionView option, bool ooh)
    {
        if (option.Inventory.Count == 0) return;
        var heading = section.AddParagraph(ooh ? "OOH site schedule" : "Media inventory schedule");
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = 8;
        var table = section.AddTable();
        table.Borders.Color = Neutral;
        table.Borders.Width = 0.4;
        foreach (var width in new[] { 4.2, 3.2, 3.5, 2.4, 4.5 }) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        SetHeader(header, "Media", "Geography", "Period", "Availability", "Client investment");
        foreach (var inventory in option.Inventory)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(inventory.Name);
            row.Cells[1].AddParagraph(inventory.Geography);
            row.Cells[2].AddParagraph(Periods(inventory.RunningPeriods));
            row.Cells[3].AddParagraph(inventory.Availability);
            row.Cells[4].AddParagraph(ProposalMoneyFormatter.Format(inventory.ClientPriceMinor, option.Currency));
        }
        AddInventoryAppendix(section, option);
    }

    private static void AddInventoryAppendix(Section section, ProposalOptionView option)
    {
        section.AddParagraph("Inventory detail", StyleNames.Heading2);
        foreach (var inventory in option.Inventory)
        {
            var title = section.AddParagraph(inventory.Name);
            title.Format.Font.Bold = true;
            title.Format.KeepWithNext = true;
            var facts = new[]
            {
                string.IsNullOrWhiteSpace(inventory.SupplierName) ? null : $"Supplier: {inventory.SupplierName}",
                $"Channel: {ProposalMediaLabels.Channel(inventory.Channel)}",
                $"Geography: {inventory.Geography}",
                $"Supply: {inventory.Availability} · {inventory.RateFreshness} · {inventory.SupplyConfidence}",
                inventory.Purchase is { } purchase
                    ? $"Purchase: {inventory.Quantity} {purchase.RateType}, rate per {purchase.Denominator}" : null,
                Deliverable(inventory),
            }.Where(value => !string.IsNullOrWhiteSpace(value));
            var paragraph = section.AddParagraph(string.Join("  |  ", facts));
            paragraph.Format.Font.Size = 8;
            paragraph.Format.Font.Color = Muted;
            foreach (var condition in inventory.CommercialTerms?.Conditions ?? [])
                AddBullet(section, $"Commercial condition: {condition}");
            foreach (var uncertainty in inventory.Uncertainties)
                AddBullet(section, $"Unresolved: {uncertainty}");
        }
    }

    private static void AddOptionLimitations(Section section, ProposalOptionView option)
    {
        var limitations = option.Inventory.SelectMany(item => item.Uncertainties).Distinct(StringComparer.Ordinal).ToArray();
        if (limitations.Length == 0) return;
        var heading = section.AddParagraph("Evidence and limitations");
        heading.Format.Font.Bold = true;
        foreach (var limitation in limitations) AddBullet(section, limitation);
    }

    private static void AddTermsAndNextStep(Section section, ProposalVersionView proposal)
    {
        section.AddParagraph("Terms and next step", StyleNames.Heading1);
        AddLabelledParagraph(section, "Terms", proposal.Terms);
        AddCallout(section,
            "Review the approved option(s) and record the client decision in Advertified before funding or booking proceeds.");
        var evidence = section.AddParagraph(
            "Prepared only from approved Advertified planning records. Rates, availability, periods and limitations remain bound to the source proposal and plan versions represented in this document.");
        evidence.Format.Font.Size = 8;
        evidence.Format.Font.Color = Muted;
    }

    private static void AddPlanningProof(Section section, ProposalCampaignContextView context)
    {
        AddCallout(section,
            $"Planning proof: {context.InventoryOptionsEvaluated} inventory options evaluated · " +
            $"{context.EligibleInventoryOptions} eligible · {context.SuppliersEvaluated} suppliers searched · " +
            $"{context.SelectedPlacements} placements carried forward.");
    }

    private static void AddSummaryRow(Table table, string label, string value)
    {
        var row = table.AddRow();
        row.Cells[0].Shading.Color = Color.Parse("#F5F7FA");
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value);
    }

    private static void SetHeader(Row row, params string[] labels)
    {
        row.Shading.Color = Color.Parse("#F1F4F8");
        row.Format.Font.Bold = true;
        for (var index = 0; index < labels.Length; index++) row.Cells[index].AddParagraph(labels[index]);
    }

    private static void AddKeyValue(Section section, string label, string value, Color brand)
    {
        var paragraph = section.AddParagraph();
        paragraph.AddFormattedText(label + "  ", TextFormat.Bold).Font.Color = brand;
        paragraph.AddText(value);
    }

    private static void AddLabelledParagraph(Section section, string label, string value)
    {
        var paragraph = section.AddParagraph();
        paragraph.AddFormattedText(label + ": ", TextFormat.Bold);
        paragraph.AddText(value);
    }

    private static void AddListField(Section section, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0) AddLabelledParagraph(section, label, string.Join(", ", values));
    }

    private static void AddBullet(Section section, string value)
    {
        var paragraph = section.AddParagraph("• " + value);
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.4);
        paragraph.Format.FirstLineIndent = Unit.FromCentimeter(-0.25);
        paragraph.Format.Font.Size = 8;
    }

    private static void AddCallout(Section section, string value)
    {
        var paragraph = section.AddParagraph(value);
        paragraph.Format.Shading.Color = Color.Parse("#F5F7FA");
        paragraph.Format.Borders.Color = Neutral;
        paragraph.Format.Borders.Width = 0.5;
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.2);
        paragraph.Format.RightIndent = Unit.FromCentimeter(0.2);
        paragraph.Format.SpaceBefore = 5;
        paragraph.Format.SpaceAfter = 7;
    }

    private static void AddLogoOrName(Cell cell, byte[]? logo, string name, bool alignRight)
    {
        if (logo is not null)
        {
            var image = cell.AddImage("base64:" + Convert.ToBase64String(logo));
            image.Height = Unit.FromCentimeter(1.15);
            image.LockAspectRatio = true;
            return;
        }
        var paragraph = cell.AddParagraph(name);
        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Size = 12;
        if (alignRight) paragraph.Format.Alignment = ParagraphAlignment.Right;
    }

    private static string? Deliverable(ProposalInventoryLineView inventory)
    {
        if (inventory.Deliverable is null) return null;
        var parts = new[]
        {
            inventory.Deliverable.Format, inventory.Deliverable.BuyingUnit,
            inventory.Deliverable.Dimensions, inventory.Deliverable.Placement,
        }.Where(value => !string.IsNullOrWhiteSpace(value));
        var value = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : "Deliverable: " + value;
    }

    private static string Periods(IEnumerable<ProposalRunningPeriodView> periods) => string.Join(", ",
        periods.Select(item => $"{item.Start:dd MMM}–{item.End:dd MMM yyyy}"));

    private static string BrandingNotice(ProposalBrandingView branding) =>
        branding.Status == ProposalBrandingStatuses.UnbrandedApproved
            ? "Unbranded proposal authorised; client brand assets remain outstanding."
            : "Approved agency and client brand assets are applied to this document.";

    private static bool IsOohProposal(ProposalVersionView proposal) =>
        proposal.Options.SelectMany(item => item.Channels).Distinct(StringComparer.Ordinal)
            .All(channel => channel is MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh);

    private static Color BrandColour(string? hex) =>
        string.IsNullOrWhiteSpace(hex) ? Color.Parse("#6B46F2") : Color.Parse(hex);
}
