using Advertified.Commercial.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Advertified API v1"));
}

app.MapGet("/", () => Results.Ok(new ServiceDescription(
    "Advertified Commercial API",
    "baseline",
    "Canonical commercial operations will be added through approved gates.")));

app.MapGet("/health/live", () => Results.Ok(new HealthResponse(
    "healthy",
    "advertified-commercial-api",
    ["process"])));

app.MapGet("/health/ready", () => Results.Ok(new HealthResponse(
    "ready",
    "advertified-commercial-api",
    ["process"])));

app.Run();

public partial class Program;
