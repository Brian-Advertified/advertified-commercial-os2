namespace Advertified.Commercial.Application.Planning;

public sealed class UnpriceableRateException : Exception
{
    public UnpriceableRateException() : base("The rate requires explicit supported buying units and valid running periods.") { }
}
