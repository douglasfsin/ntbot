using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace NtBot.Web.Services;

/// <summary>
/// Keeps the auth cookie tiny (session key only) instead of embedding the JWT + claims
/// payload. Oversized cookies were causing intermittent /app auth redirects behind
/// Cloudflare/Traefik when the encrypted ticket approached browser header limits.
/// </summary>
public sealed class MemoryAuthTicketStore : ITicketStore
{
    private static readonly ConcurrentDictionary<string, Entry> Store = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(1);
    private static long _lastSweepTicks;

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        SweepIfNeeded();
        var key = Guid.NewGuid().ToString("N");
        var expires = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(DefaultTtl);
        Store[key] = new Entry(CloneTicket(ticket), expires);
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        SweepIfNeeded();
        var expires = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(DefaultTtl);
        Store[key] = new Entry(CloneTicket(ticket), expires);
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        SweepIfNeeded();
        if (!Store.TryGetValue(key, out var entry))
            return Task.FromResult<AuthenticationTicket?>(null);

        if (entry.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            Store.TryRemove(key, out _);
            return Task.FromResult<AuthenticationTicket?>(null);
        }

        return Task.FromResult<AuthenticationTicket?>(CloneTicket(entry.Ticket));
    }

    public Task RemoveAsync(string key)
    {
        Store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private static AuthenticationTicket CloneTicket(AuthenticationTicket ticket)
    {
        var bytes = TicketSerializer.Default.Serialize(ticket);
        return TicketSerializer.Default.Deserialize(bytes)
               ?? throw new InvalidOperationException("Failed to clone authentication ticket.");
    }

    private static void SweepIfNeeded()
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now - last < 60_000)
            return;
        if (Interlocked.CompareExchange(ref _lastSweepTicks, now, last) != last)
            return;

        var cutoff = DateTimeOffset.UtcNow;
        foreach (var pair in Store)
        {
            if (pair.Value.ExpiresUtc <= cutoff)
                Store.TryRemove(pair.Key, out _);
        }
    }

    private sealed record Entry(AuthenticationTicket Ticket, DateTimeOffset ExpiresUtc);
}
