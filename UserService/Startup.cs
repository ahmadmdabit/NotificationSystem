using System.Text;

using BLL.Business;

using Common.Helpers;

using DAL.Extensions;
using DAL.Repository;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using UserService.Businesses;
using UserService.Entities;
using UserService.Repositories;

namespace UserService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));
        services.AddDAL(Configuration);

        services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "User Service API", Version = "v1" }));
        services.AddHealthChecks();
        services.AddControllers();

        var secret = Configuration["AppSettings:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "AppSettings:Secret is not configured. Generate one: openssl rand -hex 32");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            });

        services.AddHostedService<DatabaseMigration>();

        services.AddScoped<IRepository<User, long>, UserRepository>();
        services.AddScoped<IBusiness<User, long>, UserBusiness>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        if (Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT") != "true")
        {
            app.UseHttpsRedirection();
        }

        // Use forwarded headers so RemoteIpAddress is correct behind reverse proxies/gateways
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseRouting();

        app.UseCors(x => x
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithOrigins(Configuration["AppSettings:AllowedCorsOrigins"] ?? "http://localhost:8080"));

        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API V1"));

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
        });
    }
}
