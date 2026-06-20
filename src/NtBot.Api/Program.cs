using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NtBot.Api.Hubs;
using NtBot.Api.Services;
using NtBot.Api.Services.Correlation;
using NtBot.Api.Services.GammaExposure;
using NtBot.Api.Services.Interfaces;
using NtBot.Api.Services.Macro;
using NtBot.Api.Services.NinjaTrader;
using NtBot.Api.Services.Profit;
using NtBot.Api.Services.Wyckoff;
using NtBot.Api.Strategies;
using NtBot.Application;
using NtBot.Application.Queries.Health;
using NtBot.Domain.Entities;
using NtBot.Identity;
using NtBot.Infrastructure;
using NtBot.Infrastructure.Persistence;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ntbot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    var envConnection = builder.Configuration["ConnectionStrings:DefaultConnection"]
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(envConnection))
    {
        builder.Configuration["ConnectionStrings:DefaultConnection"] = envConnection;
    }

    var envJwt = Environment.GetEnvironmentVariable("JWT_SECRET");
    if (!string.IsNullOrWhiteSpace(envJwt))
    {
        builder.Configuration["Jwt:Key"] = envJwt;
    }

    builder.Host.UseSerilog();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddIdentityAuth(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "NTBot API",
            Version = "v3.0",
            Description = "Automated Trading Platform — Clean Architecture"
        });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Bearer token",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowDashboard", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://localhost:5173",
                    "http://localhost:5001",
                    "https://localhost:5001",
                    "http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io",
                    "https://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddSignalR();

    var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-minimum-32-characters-long";
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "NTBot",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "NTBotUsers",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddHttpClient("NinjaTrader", client =>
    {
        var baseUrl = builder.Configuration["NinjaTrader:ApiBaseUrl"] ?? "http://localhost:8080";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddScoped<IStrategyBase<Position>, ChochStrategy>();
    builder.Services.AddSingleton<INinjaTraderService, NinjaTraderService>();
    builder.Services.AddScoped<IWyckoffService, WyckoffService>();
    builder.Services.AddScoped<IMacroContextService, MacroContextService>();
    builder.Services.AddSingleton<IRtdService, ProfitService>();
    builder.Services.AddScoped<IGlobalCorrelationService, GlobalCorrelationService>();
    builder.Services.AddScoped<IGammaExposureService, GammaExposureService>();
    builder.Services.AddScoped<QuantStrategy>();
    builder.Services.AddHttpClient<GlobalCorrelationService>();
    builder.Services.AddScoped<GridEngine>();
    builder.Services.AddScoped<ITenantService, TenantService>();
    builder.Services.AddScoped<ITradingService, TradingService>();
    builder.Services.AddScoped<IRiskManager, RiskManager>();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NTBot API v3.0");
        c.RoutePrefix = "swagger";
    });

    app.UseSerilogRequestLogging();
    app.UseCors("AllowDashboard");

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHub<ProfitChartHub>("/hubs/profitchart");
    app.MapHub<TradingHub>("/hubs/trading");
    app.MapHub<MarketHub>("/hubs/market");
    app.MapHub<RiskHub>("/hubs/risk");
    app.MapHub<ExecutionHub>("/hubs/execution");
    app.MapHub<NotificationHub>("/hubs/notification");

    app.MapGet("/api/health", async (IMediator mediator) =>
    {
        var health = await mediator.Send(new GetHealthQuery());
        return Results.Ok(new
        {
            status = health.Status,
            timestamp = health.Timestamp,
            version = health.Version,
            services = health.Services
        });
    });

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NtBotDbContext>();
        try
        {
            Log.Information("Applying database migrations...");
            db.Database.Migrate();
            Log.Information("Database ready");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying database migrations");
        }

        try
        {
            Log.Information("Initializing ProfitChart RTD Service...");
            var rtdService = scope.ServiceProvider.GetRequiredService<IRtdService>();
            await rtdService.InitializeAsync("rtd_config.json");
            Log.Information("ProfitChart RTD Service initialized");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProfitChart RTD Service initialization failed — continuing without it");
        }
    }

    Log.Information("NTBot API v3 starting on {Urls}", builder.Configuration["urls"] ?? "http://localhost:5053");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
