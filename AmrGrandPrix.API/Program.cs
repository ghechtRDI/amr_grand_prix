var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS for container networking
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://client:5173",
            "http://localhost:3000",
            "http://localhost:4173"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Add SPA services
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "../AmrGrandPrix.Client/dist";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

// Enable CORS
app.UseCors("DevPolicy");

app.UseStaticFiles();
app.UseSpaStaticFiles();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Map health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

app.UseSpa(spa =>
{
    spa.Options.SourcePath = "../AmrGrandPrix.Client";

    if (app.Environment.IsDevelopment())
    {
        // Check if running in container (use container networking)
        var spaUrl = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            ? "http://client:5173"
            : "http://localhost:5173";
        spa.UseProxyToSpaDevelopmentServer(spaUrl);
    }
});

app.Run();
