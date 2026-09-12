using System.Net;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    private static async Task<object[]> AssertDistinctOpportunityIdentitiesAsync(HttpClient owner, Guid clientId)
    {
        var otherClient = await CreateIdentityClientAsync(owner, TenantId, "second-client");
        var otherTenantClient = await CreateIdentityClientAsync(owner, OtherTenantId, "other-tenant-client");
        var cases = new (string Name, Guid Tenant, Guid Client, string Type, string? Reference)[]
        {
            ("source-type", TenantId, clientId, "REFERRAL", "source:exact"),
            ("source-reference", TenantId, clientId, "DISCOVERY", "source:Exact"),
            ("client", TenantId, otherClient, "DISCOVERY", "source:exact"),
            ("tenant", OtherTenantId, otherTenantClient, "DISCOVERY", "source:exact"),
            ("null-reference-1", TenantId, clientId, "DISCOVERY", null),
            ("null-reference-2", TenantId, clientId, "DISCOVERY", null),
            ("empty-reference", TenantId, clientId, "DISCOVERY", "  "),
        };
        var observations = new List<object>();
        foreach (var item in cases)
        {
            using var response = await SendCommandAsync(owner,
                $"/api/v1/tenants/{item.Tenant}/opportunities", "duplicate:distinct:" + item.Name,
                DuplicateBody(item.Client, item.Type, item.Reference));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            observations.Add(new { item.Name, item.Tenant, item.Client,
                id = json.RootElement.GetProperty("id").GetGuid(), status = (int)response.StatusCode });
        }
        using var briefs = await owner.GetAsync($"/api/v1/tenants/{TenantId}/briefs");
        using var briefJson = await ReadJsonAsync(briefs);
        Assert.Empty(briefJson.RootElement.EnumerateArray());
        return observations.ToArray();
    }

    private static async Task<Guid> CreateIdentityClientAsync(HttpClient owner, Guid tenantId, string key)
    {
        using var response = await SendCommandAsync(owner,
            $"/api/v1/tenants/{tenantId}/client-accounts", "duplicate:client:" + key,
            new
            {
                externalReference = key, legalName = key, tradingName = key,
                website = "https://synthetic.example", industry = "Synthetic",
                billingProfileJson = "{}",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
