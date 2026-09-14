using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services;
using AmrGrandPrix.API.Services.LlmExtraction;
using AmrGrandPrix.API.Services.LlmExtraction.Providers;

var builder = WebApplication.CreateBuilder(args);

// Configure settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"));

// Add DbContext with PostgreSQL (or InMemory for testing)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
        options.UseInMemoryDatabase("TestDatabase");
    else
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequireLowercase       = true;
    options.Password.RequireUppercase       = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength         = 8;

    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers      = true;

    options.User.RequireUniqueEmail  = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var key = Encoding.ASCII.GetBytes(jwtSettings?.Secret ?? throw new InvalidOperationException("JWT Secret not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken            = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(key),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSettings.Issuer,
        ValidateAudience         = true,
        ValidAudience            = jwtSettings.Audience,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("ReadOnly", "Manager", "Admin"));
    options.AddPolicy("Manager",  policy => policy.RequireRole("Manager", "Admin"));
    options.AddPolicy("Admin",    policy => policy.RequireRole("Admin"));
});

// Email & Token services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// LLM extraction services
builder.Services.AddHttpClient<AnthropicLlmProvider>();
builder.Services.AddHttpClient<OllamaLlmProvider>();

var llmProvider = builder.Configuration.GetValue<string>("Llm:Provider") ?? "Anthropic";
if (llmProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ILlmProvider, OllamaLlmProvider>();
else
    builder.Services.AddScoped<ILlmProvider, AnthropicLlmProvider>();

builder.Services.AddScoped<ILlmExtractionService, LlmExtractionService>();

// Results processing services
builder.Services.AddScoped<AmrGrandPrix.API.Services.ResultsProcessing.IResultsProcessingService,
                           AmrGrandPrix.API.Services.ResultsProcessing.ResultsProcessingService>();
builder.Services.AddScoped<AmrGrandPrix.API.Services.ResultsProcessing.IRunnerMatchingService,
                           AmrGrandPrix.API.Services.ResultsProcessing.RunnerMatchingService>();

// Grand Prix calculation service
builder.Services.AddScoped<AmrGrandPrix.API.Services.GrandPrix.IGrandPrixCalculationService,
                           AmrGrandPrix.API.Services.GrandPrix.GrandPrixCalculationService>();

builder.Services.AddOpenApi();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPolicy", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://client:5173",
                "http://localhost:3000",
                "http://localhost:4173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Seed roles on startup (skip in test environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger      = scope.ServiceProvider.GetRequiredService<ILogger<RoleSeedingService>>();
        await new RoleSeedingService(roleManager, logger).SeedRolesAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding roles");
    }
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("DevPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program { }
