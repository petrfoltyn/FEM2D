using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Mapping;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Reuse the contract-defined JSON options so the wire format is identical
// for both the typed AnalysisService and the HTTP boundary.
builder.Services.Configure<JsonOptions>(o =>
{
    var defaults = ApiJsonOptions.Default;
    o.SerializerOptions.PropertyNamingPolicy = defaults.PropertyNamingPolicy;
    o.SerializerOptions.DictionaryKeyPolicy = defaults.DictionaryKeyPolicy; // null → labels stay verbatim
    o.SerializerOptions.DefaultIgnoreCondition = defaults.DefaultIgnoreCondition;
    o.SerializerOptions.NumberHandling = defaults.NumberHandling;
    o.SerializerOptions.ReadCommentHandling = defaults.ReadCommentHandling;
    o.SerializerOptions.AllowTrailingCommas = defaults.AllowTrailingCommas;
    o.SerializerOptions.AllowOutOfOrderMetadataProperties = defaults.AllowOutOfOrderMetadataProperties;
    o.SerializerOptions.WriteIndented = false;
});

builder.Services.AddSingleton<AnalysisService>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FEALiTE2D API",
        Version = "v1",
        Description = "Stateless 2D finite-element analysis service. POST a complete model, get displacements, reactions and internal forces in one response."
    });
});

const string CorsPolicy = "DcePolicy";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    if (origins.Length == 0)
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FEALiTE2D API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Meta")
   .WithSummary("Liveness probe");

app.MapPost("/api/v1/analyze", (AnalysisRequest request, AnalysisService service) =>
   {
       var response = service.Analyze(request);
       return response.Status == AnalysisStatusDto.Successful
           ? Results.Ok(response)
           : Results.UnprocessableEntity(response);
   })
   .WithTags("Analysis")
   .WithSummary("Run a full analysis on the supplied structural model.")
   .Produces<AnalysisResponse>(StatusCodes.Status200OK)
   .Produces<AnalysisResponse>(StatusCodes.Status422UnprocessableEntity);

app.Run();

public partial class Program;
