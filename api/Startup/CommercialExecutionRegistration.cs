using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Infrastructure.Booking;
using Advertified.Commercial.Infrastructure.Campaign;
using Advertified.Commercial.Infrastructure.Creative;
using Advertified.Commercial.Infrastructure.Delivery;
using Advertified.Commercial.Infrastructure.Funding;
using Advertified.Commercial.Infrastructure.Measurement;
using Microsoft.Extensions.DependencyInjection;

namespace Advertified.Commercial.Api.Startup;

// Cohesive registration of the campaign-execution stores and commands
// (booking, campaign, creative, delivery proof, performance evidence,
// measurement report and funding). Extraction from the Program.cs
// composition root keeps each startup responsibility in one owner.
internal static class CommercialExecutionRegistration
{
    internal static IServiceCollection AddCommercialExecutionStores(
        this IServiceCollection services)
    {
        services.AddScoped<BookingRecordStore>();
        services.AddScoped<IBookingReader, BookingReader>();
        services.AddScoped<IBookingCommands, BookingCommands>();
        services.AddScoped<CampaignRecordStore>();
        services.AddScoped<ICampaignReader, CampaignReader>();
        services.AddScoped<ICampaignCommands, CampaignCommands>();
        services.AddScoped<CreativeRecordStore>();
        services.AddScoped<ICreativeReader, CreativeReader>();
        services.AddScoped<ICreativeCommands, CreativeCommands>();
        services.AddScoped<DeliveryProofRecordStore>();
        services.AddScoped<IDeliveryProofReader, DeliveryProofReader>();
        services.AddScoped<IDeliveryProofCommands, DeliveryProofCommands>();
        services.AddScoped<PerformanceEvidenceRecordStore>();
        services.AddScoped<IPerformanceEvidenceReader, PerformanceEvidenceReader>();
        services.AddScoped<IPerformanceEvidenceCommands, PerformanceEvidenceCommands>();
        services.AddScoped<MeasurementReportRecordStore>();
        services.AddScoped<IMeasurementReportReader, MeasurementReportReader>();
        services.AddScoped<IMeasurementReportCommands, MeasurementReportCommands>();
        services.AddScoped<FundingRecordStore>();
        services.AddScoped<IFundingReader, FundingReader>();
        services.AddScoped<IFundingCommands, FundingCommands>();
        return services;
    }
}
