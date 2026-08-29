using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalPdfRenderer
{
    private const int PageWidth = 595;
    private const int PageHeight = 842;

    internal static RenderedProposalDocument Render(ProposalVersionView proposal)
    {
        var lines = BuildLines(proposal);
        var content = BuildContent(lines);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        };
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
            new("ADVERTIFIED", 20, true),
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
                $"Channels: {string.Join(", ", option.Channels)}",
                9, false));
            var periods = option.RunningPeriods
                .GroupBy(item => item.Channel, StringComparer.Ordinal)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(period =>
                    $"{period.Start:dd MMM}–{period.End:dd MMM yyyy}"))}");
            lines.Add(new PdfLine(string.Join(" | ", periods), 8, false));
            if (option.InventoryNames.Count > 0)
            {
                lines.Add(new PdfLine(
                    $"Media: {string.Join(", ", option.InventoryNames.Take(5))}",
                    8, false));
            }
        }
        lines.Add(new PdfLine("Terms", 11, true));
        lines.Add(new PdfLine(proposal.Terms, 8, false));
        lines.Add(new PdfLine(
            "Prepared from approved Advertified planning records. Availability and rates remain subject to the stated plan evidence and validity.",
            7, false));
        return lines;
    }

    private static string BuildContent(IReadOnlyList<PdfLine> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("0.027 0.102 0.2 rg 0 785 595 57 re f");
        var y = 810;
        foreach (var line in lines)
        {
            var font = line.Bold ? "F2" : "F1";
            var wrapWidth = line.Size >= 14 ? 54 : 88;
            foreach (var wrapped in Wrap(line.Text, wrapWidth))
            {
                builder.Append("BT /").Append(font).Append(' ').Append(line.Size)
                    .Append(" Tf ").Append(line.Bold && y > 780 ? "1 1 1" : "0.08 0.12 0.18")
                    .Append(" rg 48 ").Append(y).Append(" Td (")
                    .Append(Escape(wrapped)).AppendLine(") Tj ET");
                y -= line.Size + 6;
                if (y < 55) break;
            }
            y -= line.Bold ? 6 : 3;
            if (y < 55) break;
        }
        return builder.ToString().TrimEnd();
    }

    private static byte[] Assemble(string[] objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };
        writer.WriteLine("%PDF-1.4");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }
        var xref = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Length + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:D10} 00000 n ");
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
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
        $"{currency} {(decimal)amountMinor / 100m:N0}";

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "advertified-proposal" : safe.Trim();
    }

    private sealed record PdfLine(string Text, int Size, bool Bold);
}

internal sealed record RenderedProposalDocument(
    byte[] Content,
    string ContentHash,
    string FileName);
