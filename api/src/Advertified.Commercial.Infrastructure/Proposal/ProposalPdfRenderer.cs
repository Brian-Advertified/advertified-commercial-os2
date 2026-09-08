using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalPdfRenderer
{
    private const int PageWidth = 595;
    private const int PageHeight = 842;
    private const int BodyTop = 748;
    private const int BodyBottom = 68;

    internal static RenderedProposalDocument Render(
        ProposalVersionView proposal,
        byte[]? agencyLogo = null,
        byte[]? clientLogo = null)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
        };
        var agencyImage = AddJpegObject(objects, agencyLogo, "AgencyLogo");
        var clientImage = AddJpegObject(objects, clientLogo, "ClientLogo");
        var pageContents = BuildPageContents(
            BuildLines(proposal), proposal.Branding, agencyImage is not null, clientImage is not null);
        var pageObjectNumbers = new List<int>();
        foreach (var content in pageContents)
        {
            var pageObjectNumber = objects.Count + 1;
            var contentObjectNumber = pageObjectNumber + 1;
            pageObjectNumbers.Add(pageObjectNumber);
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >>" +
                ImageResources(agencyImage, clientImage) + " >> " +
                $"/Contents {contentObjectNumber} 0 R >>");
            objects.Add(
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\n" +
                $"stream\n{content}\nendstream");
        }
        objects[1] =
            $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] " +
            $"/Count {pageObjectNumbers.Count} >>";

        var bytes = Assemble(objects);
        return new RenderedProposalDocument(
            bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            SafeFileName(proposal.Title) + ".pdf");
    }

    private static List<PdfLine> BuildLines(ProposalVersionView proposal)
    {
        var lines = new List<PdfLine>
        {
            new($"Prepared by {proposal.Branding.AgencyName} for {proposal.Branding.ClientBrandName}", 9, true),
            new(BrandingNotice(proposal.Branding), 8, false),
            new(proposal.Title, 18, true),
            new(proposal.ExecutiveSummary, 10, false),
            new($"Valid until {proposal.ExpiryAtUtc:dd MMM yyyy}", 9, false),
        };
        foreach (var option in proposal.Options)
        {
            lines.Add(new PdfLine(option.Label, 14, true));
            lines.Add(new PdfLine(
                $"{FormatMoney(option.BudgetMinor, option.Currency)} · {option.Outcome}",
                10, false));
            lines.Add(new PdfLine(
                $"Channels: {string.Join(", ", option.Channels.Select(ProposalMediaLabels.Channel))}",
                9, false));
            var periods = option.RunningPeriods
                .GroupBy(item => item.Channel, StringComparer.Ordinal)
                .Select(group => $"{ProposalMediaLabels.Channel(group.Key)}: {string.Join(", ", group.Select(period =>
                    $"{period.Start:dd MMM}–{period.End:dd MMM yyyy}"))}");
            lines.Add(new PdfLine(string.Join(" | ", periods), 8, false));
            if (option.InventoryNames.Count > 0)
            {
                lines.Add(new PdfLine(
                    $"Media: {string.Join(", ", option.InventoryNames.Take(5))}",
                    8, false));
            }
            foreach (var inventory in option.Inventory)
            {
                lines.Add(new PdfLine(
                    $"{inventory.Name} | {inventory.Geography} | " +
                    $"{FormatMoney(inventory.ClientPriceMinor, option.Currency)} | " +
                    $"{inventory.Availability}" + (inventory.Purchase is { } purchase
                        ? $" | {inventory.Quantity} {purchase.RateType}, rate per {purchase.Denominator}" : ""),
                    8, false));
                if (inventory.Deliverable is not null)
                {
                    lines.Add(new PdfLine("Deliverable: " + string.Join(" | ", new[]
                    {
                        inventory.Deliverable.Format,
                        inventory.Deliverable.BuyingUnit,
                        inventory.Deliverable.Dimensions,
                        inventory.Deliverable.Placement,
                    }.Where(value => !string.IsNullOrWhiteSpace(value))), 7, false));
                }
                if (inventory.CommercialTerms?.Conditions is { Count: > 0 } conditions)
                {
                    lines.Add(new PdfLine(
                        "Commercial conditions: " + string.Join("; ", conditions),
                        7,
                        false));
                }
                foreach (var uncertainty in inventory.Uncertainties)
                {
                    lines.Add(new PdfLine($"Unresolved: {uncertainty}", 7, false));
                }
            }
        }
        lines.Add(new PdfLine("Terms", 11, true));
        lines.Add(new PdfLine(proposal.Terms, 8, false));
        lines.Add(new PdfLine(
            "Prepared from approved Advertified planning records. Availability and rates remain subject to the stated plan evidence and validity.",
            7, false));
        return lines;
    }

    private static List<string> BuildPageContents(
        IReadOnlyList<PdfLine> lines,
        ProposalBrandingView branding,
        bool hasAgencyLogo,
        bool hasClientLogo)
    {
        var pages = new List<StringBuilder>();
        var builder = StartPage(branding, hasAgencyLogo, hasClientLogo);
        pages.Add(builder);
        var y = BodyTop;

        foreach (var line in lines)
        {
            var font = line.Bold ? "F2" : "F1";
            var wrapWidth = line.Size >= 14 ? 54 : 88;
            foreach (var wrapped in Wrap(line.Text, wrapWidth))
            {
                if (y - line.Size - 6 < BodyBottom)
                {
                    builder = StartPage(branding, hasAgencyLogo, hasClientLogo);
                    pages.Add(builder);
                    y = BodyTop;
                }
                AppendText(builder, font, line.Size, "0.08 0.12 0.18", 48, y, wrapped);
                y -= line.Size + 6;
            }
            y -= line.Bold ? 6 : 3;
        }

        for (var index = 0; index < pages.Count; index++)
        {
            var footer = pages[index];
            footer.AppendLine("0.88 0.89 0.93 RG 48 51 m 547 51 l S");
            AppendText(
                footer,
                "F1",
                7,
                "0.36 0.39 0.47",
                48,
                34,
                $"advertified.co.za | {branding.AgencyName} | Confidential proposal | Page {index + 1} of {pages.Count}");
        }
        return pages.Select(page => page.ToString().TrimEnd()).ToList();
    }

    private static StringBuilder StartPage(
        ProposalBrandingView branding,
        bool hasAgencyLogo,
        bool hasClientLogo)
    {
        var builder = new StringBuilder();
        builder.Append(PdfColour(branding.PrimaryColour)).AppendLine(" rg 0 778 595 64 re f");
        if (hasAgencyLogo)
        {
            builder.AppendLine("1 1 1 rg 28 790 42 38 re f");
            builder.AppendLine("q 38 0 0 34 30 792 cm /AgencyLogo Do Q");
        }
        AppendText(builder, "F2", 15, "1 1 1", hasAgencyLogo ? 80 : 32, 808,
            branding.AgencyName);
        AppendText(builder, "F1", 7, "0.94 0.94 0.98", hasAgencyLogo ? 80 : 32, 794,
            $"PROPOSAL FOR {branding.ClientBrandName}");
        if (hasClientLogo)
        {
            builder.AppendLine("1 1 1 rg 517 790 48 38 re f");
            builder.AppendLine("q 44 0 0 34 519 792 cm /ClientLogo Do Q");
        }
        return builder;
    }

    private static PdfImage? AddJpegObject(List<string> objects, byte[]? content, string name)
    {
        if (content is null) return null;
        var (width, height) = JpegDimensions(content);
        var objectNumber = objects.Count + 1;
        var hex = Convert.ToHexString(content) + ">";
        objects.Add($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} " +
            $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/ASCIIHexDecode /DCTDecode] " +
            $"/Length {hex.Length} >>\nstream\n{hex}\nendstream");
        return new PdfImage(name, objectNumber);
    }

    private static string ImageResources(PdfImage? agency, PdfImage? client)
    {
        if (agency is null && client is null) return string.Empty;
        var values = new[] { agency, client }.Where(item => item is not null)
            .Select(item => $"/{item!.Name} {item.ObjectNumber} 0 R");
        return $" /XObject << {string.Join(' ', values)} >>";
    }

    private static (int Width, int Height) JpegDimensions(byte[] value)
    {
        for (var index = 2; index + 8 < value.Length;)
        {
            if (value[index] != 0xFF) { index++; continue; }
            var marker = value[index + 1];
            if (marker is >= 0xC0 and <= 0xC3)
                return ((value[index + 7] << 8) + value[index + 8],
                    (value[index + 5] << 8) + value[index + 6]);
            if (index + 3 >= value.Length) break;
            var length = (value[index + 2] << 8) + value[index + 3];
            if (length < 2) break;
            index += length + 2;
        }
        throw new ArgumentException("The approved brand asset has invalid JPEG dimensions.");
    }

    private static string BrandingNotice(ProposalBrandingView branding) =>
        branding.Status == ProposalBrandingStatuses.UnbrandedApproved
            ? "Unbranded proposal authorised; client brand assets remain outstanding."
            : "Approved agency and client brand assets are applied to this document.";

    private static string PdfColour(string? hex)
    {
        if (hex is null) return "0.36 0.18 0.95";
        var red = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var green = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var blue = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return FormattableString.Invariant($"{red:0.###} {green:0.###} {blue:0.###}");
    }

    private static void AppendText(
        StringBuilder builder,
        string font,
        int size,
        string colour,
        int x,
        int y,
        string value)
    {
        builder.Append("BT /").Append(font).Append(' ').Append(size)
            .Append(" Tf ").Append(colour).Append(" rg ")
            .Append(x).Append(' ').Append(y).Append(" Td (")
            .Append(Escape(value)).AppendLine(") Tj ET");
    }

    private static byte[] Assemble(List<string> objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };
        writer.WriteLine("%PDF-1.4");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }
        var xref = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:D10} 00000 n ");
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xref.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static IEnumerable<string> Wrap(string value, int width)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal)
        .Replace("–", "-", StringComparison.Ordinal)
        .Replace("·", "-", StringComparison.Ordinal);

    private static string FormatMoney(long amountMinor, string currency) =>
        ProposalMoneyFormatter.Format(amountMinor, currency);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "advertified-proposal" : safe.Trim();
    }

    private sealed record PdfLine(string Text, int Size, bool Bold);
    private sealed record PdfImage(string Name, int ObjectNumber);
}

internal sealed record RenderedProposalDocument(
    byte[] Content,
    string ContentHash,
    string FileName);
