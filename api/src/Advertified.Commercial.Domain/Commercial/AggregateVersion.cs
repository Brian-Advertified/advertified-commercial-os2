namespace Advertified.Commercial.Domain.Commercial;

internal static class AggregateVersion
{
    public static long Next(long current, long expected)
    {
        if (current != expected)
        {
            throw new VersionConflictException();
        }

        return checked(current + 1);
    }
}
