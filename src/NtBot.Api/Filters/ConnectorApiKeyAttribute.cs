using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Connector.Services;

namespace NtBot.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ConnectorApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HttpContextTenantIdKey = "ConnectorTenantId";
    public const string HttpContextKeyIdKey = "ConnectorKeyId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var connector = context.HttpContext.RequestServices.GetRequiredService<IConnectorService>();
        var apiKey = context.HttpContext.Request.Headers["X-Connector-Api-Key"].FirstOrDefault()
            ?? context.HttpContext.Request.Query["apiKey"].FirstOrDefault();

        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var auth = await connector.ValidateApiKeyAsync(apiKey ?? string.Empty, ip);

        if (!auth.Success || !auth.LicenseActive)
        {
            context.Result = new UnauthorizedObjectResult(new { message = auth.Error ?? "ApiKey inválida." });
            return;
        }

        context.HttpContext.Items[HttpContextTenantIdKey] = auth.TenantId;
        context.HttpContext.Items[HttpContextKeyIdKey] = auth.KeyId;
        await next();
    }
}
