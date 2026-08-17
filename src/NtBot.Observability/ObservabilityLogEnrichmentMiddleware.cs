using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace NtBot.Observability;

public sealed class ObservabilityLogEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value ?? "anonymous";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        using (LogContext.PushProperty("tenant.id", tenantId))
        using (LogContext.PushProperty("user.id", userId))
        using (LogContext.PushProperty("trace_id", Activity.Current?.TraceId.ToString() ?? ""))
        {
            await next(context);
        }
    }
}
