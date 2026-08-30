namespace Advertified.Commercial.Infrastructure.Booking;

internal static class BookingPolicy
{
    private const int MaximumTermsLength = 5_000;
    private const int MaximumReasonLength = 1_000;
    private const int MaximumNoteLength = 2_000;

    internal static string RequiredTerms(string value) =>
        Required(value, MaximumTermsLength, "Booking terms");

    internal static string RequiredReason(string value) =>
        Required(value, MaximumReasonLength, "Booking reason");

    internal static string? OptionalNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        return result.Length <= MaximumNoteLength
            ? result
            : throw new ArgumentException("The supplier note is too long.", nameof(value));
    }

    private static string Required(string value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.", nameof(value));
        }
        var result = value.Trim();
        return result.Length <= maximumLength
            ? result
            : throw new ArgumentException($"{label} is too long.", nameof(value));
    }
}
