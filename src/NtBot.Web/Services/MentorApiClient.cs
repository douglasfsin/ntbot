using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class MentorApiClient : AuthenticatedApiClient
{
    public MentorApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<MentorSnapshotModel?> GetSnapshotAsync() =>
        GetAsync<MentorSnapshotModel>("api/mentor/snapshot", authenticated: true);
}
