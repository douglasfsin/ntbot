using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Billing.Options;
using Stripe;
using Stripe.Checkout;

namespace NtBot.Billing.Services;

public interface IStripeGatewayService
{
    bool IsConfigured { get; }
    bool IsTestMode { get; }
    string? PublishableKey { get; }
    Task<Session?> CreateCheckoutSessionAsync(CheckoutSessionParams parameters, CancellationToken ct = default);
    Task<Session?> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default);
    Task<bool> CancelSubscriptionAsync(string stripeSubscriptionId, bool atPeriodEnd = true, CancellationToken ct = default);
    Event ConstructWebhookEvent(string json, string signatureHeader);
}

public record CheckoutSessionParams(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    decimal Amount,
    string Currency,
    string PayerEmail,
    string PayerName,
    int? TrialDays);

public class StripeGatewayService : IStripeGatewayService
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeGatewayService> _logger;

    public StripeGatewayService(IOptions<StripeSettings> settings, ILogger<StripeGatewayService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.SecretKey))
            StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.SecretKey) &&
        !string.IsNullOrWhiteSpace(_settings.BackUrl) &&
        _settings.SecretKey.StartsWith("sk_", StringComparison.Ordinal);

    public bool IsTestMode => _settings.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal);

    public string? PublishableKey =>
        string.IsNullOrWhiteSpace(_settings.PublishableKey) ? null : _settings.PublishableKey;

    public async Task<Session?> CreateCheckoutSessionAsync(
        CheckoutSessionParams parameters,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("[Stripe] SecretKey/BackUrl não configurados — checkout indisponível");
            return null;
        }

        var amountCents = (long)(parameters.Amount * 100);
        var currency = parameters.Currency.ToLowerInvariant();

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            ClientReferenceId = parameters.SubscriptionId.ToString(),
            CustomerEmail = parameters.PayerEmail,
            SuccessUrl = $"{_settings.BackUrl.TrimEnd('/')}/billing/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_settings.BackUrl.TrimEnd('/')}/billing/cancel",
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = amountCents,
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month",
                            IntervalCount = 1
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"NTBot — {parameters.PlanName}",
                            Description = $"Assinatura mensal {parameters.PlanName}"
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["subscription_id"] = parameters.SubscriptionId.ToString(),
                ["plan_id"] = parameters.PlanId.ToString(),
                ["tenant_email"] = parameters.PayerEmail
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["subscription_id"] = parameters.SubscriptionId.ToString(),
                    ["plan_id"] = parameters.PlanId.ToString()
                },
                TrialPeriodDays = parameters.TrialDays is > 0 ? parameters.TrialDays : null
            }
        };

        _logger.LogInformation(
            "[Stripe] Criando checkout | SubscriptionId={SubscriptionId} Plan={Plan} Amount={Amount} {Currency}",
            parameters.SubscriptionId, parameters.PlanName, parameters.Amount, currency.ToUpperInvariant());

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation("[Stripe] Checkout criado | SessionId={SessionId} Url={Url}", session.Id, session.Url);
        return session;
    }

    public async Task<Session?> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        var service = new SessionService();
        return await service.GetAsync(sessionId, cancellationToken: ct);
    }

    public async Task<bool> CancelSubscriptionAsync(
        string stripeSubscriptionId,
        bool atPeriodEnd = true,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        var service = new SubscriptionService();
        if (atPeriodEnd)
        {
            await service.UpdateAsync(stripeSubscriptionId, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            }, cancellationToken: ct);
        }
        else
        {
            await service.CancelAsync(stripeSubscriptionId, cancellationToken: ct);
        }

        _logger.LogInformation("[Stripe] Subscription cancelada | StripeSubId={StripeSubId} AtPeriodEnd={AtPeriodEnd}",
            stripeSubscriptionId, atPeriodEnd);
        return true;
    }

    public Event ConstructWebhookEvent(string json, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
        {
            _logger.LogWarning("[Stripe] WebhookSecret não configurado — parse sem verificação de assinatura");
            return EventUtility.ParseEvent(json);
        }

        return EventUtility.ConstructEvent(json, signatureHeader, _settings.WebhookSecret);
    }
}
