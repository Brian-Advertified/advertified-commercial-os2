# Versioned Commercial API contract

`advertified-commercial-api.v1.json` is generated from the Release API assembly. It is
retained so public contract changes are reviewable and browser schemas can be derived from
one canonical source.

From the repository root after a Release build:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__CommercialDatabase = 'Host=localhost;Database=openapi-contract;Username=openapi-contract'
dotnet swagger tofile --output shared/contracts/openapi/advertified-commercial-api.v1.json api/bin/Release/net10.0/Advertified.Commercial.Api.dll v1
Remove-Item Env:ASPNETCORE_ENVIRONMENT
Remove-Item Env:ConnectionStrings__CommercialDatabase
```

Do not hand-edit the generated JSON. Regenerate it and run the API contract test.
