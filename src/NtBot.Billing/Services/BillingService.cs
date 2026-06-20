using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Billing.Dtos;
using NtBot.Billing.Options;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using Stripe.Checkout;
using DomainSubscription = NtBot.Domain.Entities.Subscription;
using DomainPlan = NtBot.Domain.Entities.Plan;

namespace NtBot.Billing.Services;

public interface IBillingService
{
    Task<BillingConfigDto> GetConfigAsync(CancellationToken ct = default);
    Task<List<PlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task<SubscriptionDto?> GetTenantSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
    Task<CheckoutResponse> CreateCheckoutAsync(
        Guid tenantId,
        string userEmail,
        string? userName,
        CheckoutRequest request,
        CancellationToken ct = default);
    Task<CheckoutResponse> ConfirmCheckoutSessionAsync(
        Guid tenantId,
        string sessionId,
        CancellationToken ct = default);
    Task<bool> ProcessStripeWebhookAsync(string json, string signatureHeader, CancellationToken ct = default);
    Task<bool> CancelSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
}

public class BillingService : IBillingService
{
    private readonly NtBotDbContext _db;
    private readonly IStripeGatewayService _stripe;
    private readonly SubscriptionSettings _subscriptionSettings;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        NtBotDbContext db,
        IStripeGatewayService stripe,
        IOptions<SubscriptionSettings> subscriptionSettings,
        ILogger<BillingService> logger)
    {
        _db = db;
        _stripe = stripe;
        _subscriptionSettings = subscriptionSettings.Value;
        _logger = logger;
    }

    public Task<BillingConfigDto> GetConfigAsync(CancellationToken ct = default) =>
        Task.FromResult(new BillingConfigDto
        {
            StripeConfigured = _stripe.IsConfigured,
            PublishableKey = _stripe.PublishableKey,
            TestMode = _stripe.IsTestMode
        });

    public async Task<List<PlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        return await _db.Plans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanDto
            {
                Id = p.Id,
                Slug = p.Slug,
                DisplayName = p.DisplayName,
                Description = p.Description,
                MonthlyPrice = p.MonthlyPrice,
                YearlyPrice = p.YearlyPrice,
                Currency = p.Currency,
                MaxStrategies = p.MaxStrategies,
                MaxBrokers = p.MaxBrokers,
                MaxActivePositions = p.MaxActivePositions
            })
            .ToListAsync(ct);
    }

    public async Task<SubscriptionDto?> GetTenantSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (subscription == null) return null;

        return MapSubscription(subscription);
    }

    public async Task<CheckoutResponse> CreateCheckoutAsync(
        Guid tenantId,
        string userEmail,
        string? userName,
        CheckoutRequest request,
        CancellationToken ct = default)
    {
        if (!_stripe.IsConfigured)
        {
            _logger.LogWarning("[Billing] Stripe não configurado — defina Stripe__SecretKey e Stripe__BackUrl");
            return Fail("Pagamentos Stripe não configurados neste ambiente.");
        }

        var plan = await _db.Plans
            .FirstOrDefaultAsync(p => p.Slug == request.PlanSlug && p.IsActive, ct);

        if (plan == null)
            return Fail("Plano não encontrado.");

        if (plan.MonthlyPrice <= 0)
            return Fail("O plano Free não requer checkout.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant == null)
            return Fail("Tenant não encontrado.");

        var subscription = new DomainSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatuses.Pending,
            PaymentStatus = PaymentStatuses.Pending,
            PaymentGateway = "stripe",
            MonthlyPrice = plan.MonthlyPrice,
            StartDate = DateTime.UtcNow,
            GracePeriodDays = _subscriptionSettings.GracePeriodDays,
            AutoRenew = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        var trialDays = tenant.IsTrial ? _subscriptionSettings.TrialDays : (int?)null;

        Session? session;
        try
        {
            session = await _stripe.CreateCheckoutSessionAsync(new CheckoutSessionParams(
                subscription.Id,
                plan.Id,
                plan.DisplayName,
                plan.MonthlyPrice,
                plan.Currency,
                userEmail,
                userName ?? tenant.Name,
                trialDays), ct);
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "[Billing] Erro Stripe ao criar checkout para tenant {TenantId}", tenantId);
            _db.Subscriptions.Remove(subscription);
            await _db.SaveChangesAsync(ct);
            return Fail($"Erro ao criar sessão Stripe: {ex.Message}");
        }

        if (session == null || string.IsNullOrEmpty(session.Url))
        {
            _db.Subscriptions.Remove(subscription);
            await _db.SaveChangesAsync(ct);
            return Fail("Stripe não retornou URL de checkout.");
        }

        subscription.StripeCheckoutSessionId = session.Id;
        subscription.StripeCustomerId = session.CustomerId;
        subscription.StripeSubscriptionId = session.SubscriptionId;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[Billing] Checkout iniciado | TenantId={TenantId} SubscriptionId={SubscriptionId} SessionId={SessionId}",
            tenantId, subscription.Id, session.Id);

        return new CheckoutResponse
        {
            Success = true,
            CheckoutUrl = session.Url,
            SubscriptionId = subscription.Id,
            Message = "Redirecionando para pagamento Stripe"
        };
    }

    public async Task<CheckoutResponse> ConfirmCheckoutSessionAsync(
        Guid tenantId,
        string sessionId,
        CancellationToken ct = default)
    {
        var session = await _stripe.GetCheckoutSessionAsync(sessionId, ct);
        if (session == null)
            return Fail("Sessão Stripe não encontrada.");

        if (!Guid.TryParse(session.ClientReferenceId, out var subscriptionId))
            return Fail("Referência de assinatura inválida.");

        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.TenantId == tenantId, ct);

        if (subscription == null)
            return Fail("Assinatura não encontrada para este tenant.");

        await ActivateSubscriptionFromSessionAsync(subscription, session, ct);

        return new CheckoutResponse
        {
            Success = true,
            SubscriptionId = subscription.Id,
            Message = "Assinatura confirmada"
        };
    }

    public async Task<bool> ProcessStripeWebhookAsync(
        string json,
        string signatureHeader,
        CancellationToken ct = default)
    {
        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = _stripe.ConstructWebhookEvent(json, signatureHeader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Billing] Webhook Stripe inválido");
            return false;
        }

        if (await _db.WebhookEvents.AnyAsync(e => e.EventId == stripeEvent.Id, ct))
        {
            _logger.LogInformation("[Billing] Webhook duplicado ignorado | EventId={EventId}", stripeEvent.Id);
            return true;
        }

        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Gateway = "stripe",
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Status = "processing",
            Payload = json,
            CreatedAt = DateTime.UtcNow
        };
        _db.WebhookEvents.Add(webhookEvent);

        try
        {
            await HandleStripeEventAsync(stripeEvent, webhookEvent, ct);
            webhookEvent.Status = "processed";
            webhookEvent.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            webhookEvent.Status = "failed";
            webhookEvent.ErrorMessage = ex.Message;
            webhookEvent.RetryCount += 1;
            _logger.LogError(ex, "[Billing] Falha ao processar webhook {EventType} | EventId={EventId}",
                stripeEvent.Type, stripeEvent.Id);
            await _db.SaveChangesAsync(ct);
            return false;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .Include(s => s.Tenant)
            .Where(s => s.TenantId == tenantId && s.Status != SubscriptionStatuses.Cancelled)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (subscription == null) return false;

        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            await _stripe.CancelSubscriptionAsync(subscription.StripeSubscriptionId, atPeriodEnd: true, ct);

        subscription.Status = SubscriptionStatuses.Cancelled;
        subscription.PaymentStatus = PaymentStatuses.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        subscription.Tenant.Plan = SubscriptionPlan.FREE;
        subscription.Tenant.IsTrial = false;
        subscription.Tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task HandleStripeEventAsync(Stripe.Event stripeEvent, WebhookEvent webhookEvent, CancellationToken ct)
    {
        _logger.LogInformation("[Billing] Webhook Stripe | Type={Type} EventId={EventId}",
            stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case Stripe.EventTypes.CheckoutSessionCompleted:
                if (stripeEvent.Data.Object is Session session)
                    await HandleCheckoutCompletedAsync(session, webhookEvent, ct);
                break;

            case Stripe.EventTypes.CustomerSubscriptionUpdated:
            case Stripe.EventTypes.CustomerSubscriptionCreated:
                if (stripeEvent.Data.Object is Stripe.Subscription stripeSub)
                    await HandleSubscriptionUpdatedAsync(stripeSub, webhookEvent, ct);
                break;

            case Stripe.EventTypes.CustomerSubscriptionDeleted:
                if (stripeEvent.Data.Object is Stripe.Subscription deletedSub)
                    await HandleSubscriptionDeletedAsync(deletedSub, webhookEvent, ct);
                break;

            case Stripe.EventTypes.InvoicePaid:
                if (stripeEvent.Data.Object is Stripe.Invoice invoice)
                    await HandleInvoicePaidAsync(invoice, webhookEvent, ct);
                break;

            case Stripe.EventTypes.InvoicePaymentFailed:
                if (stripeEvent.Data.Object is Stripe.Invoice failedInvoice)
                    await HandleInvoiceFailedAsync(failedInvoice, webhookEvent, ct);
                break;

            default:
                _logger.LogInformation("[Billing] Evento Stripe ignorado | Type={Type}", stripeEvent.Type);
                webhookEvent.Status = "ignored";
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Session session, WebhookEvent webhookEvent, CancellationToken ct)
    {
        if (!Guid.TryParse(session.ClientReferenceId, out var subscriptionId))
            throw new InvalidOperationException("client_reference_id ausente no checkout session.");

        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException($"Subscription {subscriptionId} não encontrada.");

        webhookEvent.RelatedEntityId = subscription.Id;
        webhookEvent.RelatedEntityType = nameof(DomainSubscription);

        await ActivateSubscriptionFromSessionAsync(subscription, session, ct);
    }

    private async Task ActivateSubscriptionFromSessionAsync(DomainSubscription subscription, Session session, CancellationToken ct)
    {
        subscription.StripeCheckoutSessionId = session.Id;
        subscription.StripeCustomerId = session.CustomerId ?? subscription.StripeCustomerId;
        subscription.StripeSubscriptionId = session.SubscriptionId ?? subscription.StripeSubscriptionId;
        subscription.Status = SubscriptionStatuses.Active;
        subscription.PaymentStatus = session.PaymentStatus == "paid"
            ? PaymentStatuses.Paid
            : PaymentStatuses.Pending;
        subscription.LastPaymentDate = DateTime.UtcNow;
        subscription.NextPaymentDate = DateTime.UtcNow.AddDays(30);
        subscription.UpdatedAt = DateTime.UtcNow;

        ApplyPlanToTenant(subscription.Tenant, subscription.Plan);
        subscription.Tenant.StripeCustomerId = subscription.StripeCustomerId;
        subscription.Tenant.IsTrial = false;
        subscription.Tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[Billing] Assinatura ativada | SubscriptionId={SubscriptionId} TenantId={TenantId} Plan={Plan}",
            subscription.Id, subscription.TenantId, subscription.Plan.Slug);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        Stripe.Subscription stripeSub,
        WebhookEvent webhookEvent,
        CancellationToken ct)
    {
        var subscription = await FindSubscriptionByStripeIdAsync(stripeSub.Id, stripeSub.Metadata, ct);
        if (subscription == null) return;

        webhookEvent.RelatedEntityId = subscription.Id;
        webhookEvent.RelatedEntityType = nameof(DomainSubscription);

        subscription.StripeSubscriptionId = stripeSub.Id;
        subscription.StripeCustomerId = stripeSub.CustomerId;
        subscription.Status = MapStripeSubscriptionStatus(stripeSub.Status);
        subscription.PaymentStatus = stripeSub.Status == "trialing"
            ? PaymentStatuses.Trialing
            : stripeSub.Status == "active" ? PaymentStatuses.Paid : PaymentStatuses.Pending;
        subscription.CurrentPeriodStart = stripeSub.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = stripeSub.CurrentPeriodEnd;
        subscription.TrialEnd = stripeSub.TrialEnd;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (subscription.IsActive())
            ApplyPlanToTenant(subscription.Tenant, subscription.Plan);

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionDeletedAsync(
        Stripe.Subscription stripeSub,
        WebhookEvent webhookEvent,
        CancellationToken ct)
    {
        var subscription = await FindSubscriptionByStripeIdAsync(stripeSub.Id, stripeSub.Metadata, ct);
        if (subscription == null) return;

        webhookEvent.RelatedEntityId = subscription.Id;
        webhookEvent.RelatedEntityType = nameof(DomainSubscription);

        subscription.Status = SubscriptionStatuses.Cancelled;
        subscription.PaymentStatus = PaymentStatuses.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.EndDate = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        subscription.Tenant.Plan = SubscriptionPlan.FREE;
        subscription.Tenant.IsTrial = false;
        subscription.Tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoicePaidAsync(Stripe.Invoice invoice, WebhookEvent webhookEvent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(invoice.SubscriptionId)) return;

        var subscription = await FindSubscriptionByStripeIdAsync(invoice.SubscriptionId, invoice.Metadata, ct);
        if (subscription == null) return;

        webhookEvent.RelatedEntityId = subscription.Id;
        webhookEvent.RelatedEntityType = nameof(DomainSubscription);

        if (await _db.BillingHistories.AnyAsync(b => b.StripeInvoiceId == invoice.Id, ct))
            return;

        _db.BillingHistories.Add(new BillingHistory
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            StripeInvoiceId = invoice.Id,
            Amount = (decimal)(invoice.AmountPaid / 100.0),
            Currency = invoice.Currency?.ToUpperInvariant() ?? "USD",
            Status = "paid",
            PaidAt = invoice.StatusTransitions?.PaidAt ?? DateTime.UtcNow,
            InvoiceUrl = invoice.HostedInvoiceUrl,
            CreatedAt = DateTime.UtcNow
        });

        subscription.PaymentStatus = PaymentStatuses.Paid;
        subscription.LastPaymentDate = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoiceFailedAsync(Stripe.Invoice invoice, WebhookEvent webhookEvent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(invoice.SubscriptionId)) return;

        var subscription = await FindSubscriptionByStripeIdAsync(invoice.SubscriptionId, invoice.Metadata, ct);
        if (subscription == null) return;

        webhookEvent.RelatedEntityId = subscription.Id;
        webhookEvent.RelatedEntityType = nameof(DomainSubscription);

        subscription.PaymentStatus = PaymentStatuses.Overdue;
        subscription.Status = SubscriptionStatuses.Suspended;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<DomainSubscription?> FindSubscriptionByStripeIdAsync(
        string stripeSubscriptionId,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        DomainSubscription? subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);

        if (subscription != null) return subscription;

        if (metadata != null &&
            metadata.TryGetValue("subscription_id", out var subIdStr) &&
            Guid.TryParse(subIdStr, out var subId))
        {
            subscription = await _db.Subscriptions
                .Include(s => s.Plan)
                .Include(s => s.Tenant)
                .FirstOrDefaultAsync(s => s.Id == subId, ct);
        }

        return subscription;
    }

    private static void ApplyPlanToTenant(Tenant tenant, DomainPlan plan)
    {
        tenant.Plan = plan.Slug switch
        {
            "pro" => SubscriptionPlan.PRO,
            "enterprise" => SubscriptionPlan.ENTERPRISE,
            _ => SubscriptionPlan.FREE
        };
        tenant.MaxActivePositions = plan.MaxActivePositions;
        tenant.SubscriptionStart ??= DateTime.UtcNow;
        tenant.SubscriptionEnd = null;
    }

    private static string MapStripeSubscriptionStatus(string stripeStatus) => stripeStatus switch
    {
        "active" => SubscriptionStatuses.Active,
        "trialing" => SubscriptionStatuses.Trialing,
        "past_due" => SubscriptionStatuses.Suspended,
        "canceled" => SubscriptionStatuses.Cancelled,
        "unpaid" => SubscriptionStatuses.Suspended,
        _ => SubscriptionStatuses.Pending
    };

    private static SubscriptionDto MapSubscription(DomainSubscription subscription) => new()
    {
        Id = subscription.Id,
        PlanId = subscription.PlanId,
        PlanSlug = subscription.Plan.Slug,
        PlanName = subscription.Plan.DisplayName,
        Status = subscription.Status,
        PaymentStatus = subscription.PaymentStatus,
        MonthlyPrice = subscription.MonthlyPrice,
        CurrentPeriodEnd = subscription.CurrentPeriodEnd,
        TrialEnd = subscription.TrialEnd,
        IsActive = subscription.IsActive()
    };

    private static CheckoutResponse Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
