using System.Text.Json;
using System.Text.Json.Serialization;
using Kandinsky.Server.Diagrams;
using Kandinsky.Server.Endpoints;
using Kandinsky.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.AllowDuplicateProperties = false;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddSingleton(sp =>
{
    var configured = sp.GetRequiredService<IConfiguration>()["Storage:Path"] ?? "data";
    return new DiagramFileStore(Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, configured));
});
builder.Services.AddSingleton<DiagramService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapDiagramEndpoints();

app.Run();
