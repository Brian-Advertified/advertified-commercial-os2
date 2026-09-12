using System.Security.Cryptography;
using Advertified.Commercial.Application.Proposal;
using MigraDoc.Rendering;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalPdfRenderer
{
    internal static RenderedProposalDocument Render(
        ProposalVersionView proposal,
        byte[]? agencyLogo = null,
        byte[]? clientLogo = null,
        ProposalCampaignContextView? campaignContext = null)
    {
        ProposalFontResolver.EnsureConfigured();
        var document = ProposalDocumentBuilder.Build(
            proposal, agencyLogo, clientLogo, campaignContext ?? proposal.CampaignContext);
        var renderer = new PdfDocumentRenderer
        {
            Document = document,
        };
        renderer.PdfDocument.Options.CompressContentStreams = true;
        renderer.PdfDocument.ViewerPreferences.FitWindow = true;
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        var bytes = stream.ToArray();
        return new RenderedProposalDocument(
            bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            SafeFileName(proposal.Title) + ".pdf");
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character =>
            invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "advertified-proposal" : safe.Trim();
    }
}

internal sealed record RenderedProposalDocument(
    byte[] Content,
    string ContentHash,
    string FileName);
