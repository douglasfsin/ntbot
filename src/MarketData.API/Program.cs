using MarketData.API.Configuration;
using MarketData.API.MT5Worker;
using MarketData.API.SignalR;
using MarketData.API.Worker;
using MarketData.API.Worker.Simulator;
using Marketdata.Database;
using System;
using Orbital.Core;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Configurações
// -----------------------------------------------------------------------
builder.Services.Configure<ProfitDllSettings>(
    builder.Configuration.GetSection("ProfitDLL"));

builder.Services.Configure<MT5Settings>(
    builder.Configuration.GetSection("MT5"));

// -----------------------------------------------------------------------
// Simulador de dados (Mock)
// -----------------------------------------------------------------------
//var useMock = builder.Configuration.GetValue<bool>("UseMockSimulator");
//if (useMock)
//{
builder.Services.AddSingleton<IMarketDataProvider, MockMarketDataProvider>();
//}
//else
//{
//    // For real mode, use MarketDataWorker as IMarketDataProvider
//    // Will be registered after the worker
//}

builder.Services.AddSingleton<MarketData.API.Services.ProfitHistoricalTradeProvider>();
builder.Services.AddSingleton<Marketdata.Database.Services.IMarketDataHistoryProvider>(sp => sp.GetRequiredService<MarketData.API.Services.ProfitHistoricalTradeProvider>());

// Register concrete singleton first, then host it — avoids GetServices<IHostedService>()
// lookup that can re-enter construction. Lazy<MarketDataWorker> breaks the SignalR cycle:
// MarketHub → Worker → IHubContext<MarketHub>.
builder.Services.AddSingleton<MarketDataWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MarketDataWorker>());
builder.Services.AddTransient(provider =>
    new Lazy<MarketDataWorker>(() => provider.GetRequiredService<MarketDataWorker>()));

//if (!useMock)
//{
//builder.Services.AddSingleton<IMarketDataProvider>(provider => provider.GetRequiredService<MarketDataWorker>());
//}

// -----------------------------------------------------------------------
// MT5 Worker e Hub
// -----------------------------------------------------------------------
builder.Services.AddSingleton<MT5Worker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MT5Worker>());

//// -----------------------------------------------------------------------
//// Persistência QuestDB
//// -----------------------------------------------------------------------
//builder.Services.AddMarketDataPersistence(builder.Configuration);
//builder.Services.AddHostedService<PersistenceWorker>();

// Registra SMCQuestDbClient (QuestDB via Npgsql) para o TrendWorker
//builder.Services.AddSingleton<Orbital.Core.Services.SMCQuestDbClient>(sp =>
//{
//    var logger = sp.GetRequiredService<ILogger<Orbital.Core.Services.SMCQuestDbClient>>();
//    return new Orbital.Core.Services.SMCQuestDbClient("Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest;Server Compatibility Mode=NoTypeLoading;", logger);
//});
//builder.Services.AddHostedService<MarketData.API.Worker.TrendWorker>();

// Registra serviços e HostedService do TapeSpeed Engine
//builder.Services.AddSingleton<Orbital.Core.Services.TapeSpeedAnalyzer>();
//builder.Services.AddSingleton<Orbital.Core.Services.TapeSpeedDbClient>(provider => 
//{
//    var logger = provider.GetRequiredService<ILogger<Orbital.Core.Services.TapeSpeedDbClient>>();
//    return new Orbital.Core.Services.TapeSpeedDbClient("Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest;Server Compatibility Mode=NoTypeLoading;", logger);
//});
//builder.Services.AddSingleton<Orbital.Core.Services.TapeSpeedEngine>();
//builder.Services.AddHostedService<Orbital.Core.Services.TapeSpeedEngine>(provider => provider.GetRequiredService<Orbital.Core.Services.TapeSpeedEngine>());
//builder.Services.AddHostedService<MarketData.API.Worker.TapeSpeedWorker>();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddHttpClient("MT5Python", client =>
{
    // Timeout alto — SSE é uma conexão de longa duração
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});
// Porta 5242 = HTTPS apenas. Clients devem usar https://localhost:5242
// (HTTP plain causa ResponseEnded / connection closed — NtBot.Api ProfitService).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5242, listen => listen.UseHttps());
});

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields =
        Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpLogging();
//app.UseHttpsRedirection();
app.UseAuthorization();
app.UseRouting();
app.UseCors();
app.MapHub<MarketHub>("/marketHub");
app.MapHub<MT5Hub>("/mt5Hub");
app.MapControllers();

app.MapGet("/signarl", async (HttpContext ctx, IWebHostEnvironment env) =>
{
    var file = Path.Combine(env.ContentRootPath, "Signarl.html");
    if (!File.Exists(file))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.SendFileAsync(file);
});

app.Run();
