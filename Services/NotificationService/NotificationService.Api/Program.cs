using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using NotificationService.Application;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Dapper + Infrastructure
builder.Services.AddNotificationServiceInfrastructure(builder.Configuration);

// Application layer (MediatR, FluentValidation, behaviors)
builder.Services.AddNotificationServiceApplication();

// JWT Auth
var secret = builder.Configuration["AppSettings:Secret"];
if (string.IsNullOrWhiteSpace(secret))
    throw new InvalidOperationException("AppSettings:Secret is not configured. Generate one: openssl rand -hex 64");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    });

// Authorization: authenticated-by-default, role policies for destructive endpoints
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("Service", policy => policy.RequireRole("service"));
});

// CORS - named policy configured once at startup
var corsOrigins = (builder.Configuration["AppSettings:AllowedCorsOrigins"] ?? "http://localhost:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length == 0)
    throw new InvalidOperationException("AppSettings:AllowedCorsOrigins must contain at least one origin");

builder.Services.AddCors(options =>
{
    options.AddPolicy("ui", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers (ApiController model validation kept OFF as single-source: FluentValidation
// owns all input rules; ApiExceptionHandler translates ValidationException -> 400 ApiResult)
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);

// Centralized exception -> ApiResult translation (ValidationException->400,
// NotFound->404, Duplicate->400, else 500 envelope)
builder.Services.AddExceptionHandler<Shared.Api.ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Swagger
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" }));

// Health checks: liveness plus broker reachability when RabbitMQ transport is enabled
builder.Services.AddHealthChecks()
    .AddCheck<Shared.Api.MessagingHealthCheck>("messaging");

// Database migration hosted service
builder.Services.AddHostedService<NotificationService.Infrastructure.DatabaseMigration>();

var app = builder.Build();

// ApiResult envelope in every environment (no developer exception page). HSTS is
// transport hardening, not error handling, so it stays non-Development-only (N-05).
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (builder.Configuration["DISABLE_HTTPS_REDIRECT"] != "true")
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("ui");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API V1"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposes the entry point to WebApplicationFactory<Program> for endpoint
// smoke/integration tests (L-12). Top-level Program is synthesized internal;
// this partial declaration makes the type public.
public partial class Program { }
