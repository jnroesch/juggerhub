using System.Net;
using System.Text;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Stands in for tugeny.org underneath the real named <c>"Tugeny"</c> client — so the shipped
/// resilience pipeline, size guard and parser all run — serving the committed real responses by
/// path. Counts every transport hit, retries included.
/// </summary>
internal sealed class TugenyStub : HttpMessageHandler
{
    private readonly List<(string Prefix, Func<HttpResponseMessage> Respond)> _routes = [];
    private Func<HttpResponseMessage>? _all;
    private bool _hang;
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>The 25th German championship, finalized: 20 teams, 78 matches (9 draws).</summary>
    public static TugenyStub Finalized() => new TugenyStub()
        .Serve("/api/persistent/tournamentsBySlug/", ResultsTestSupport.Fixture("tournament-by-slug.finalized.json"))
        .Serve("/api/persistent/rankingsByTournamentId/", ResultsTestSupport.Fixture("rankings.finalized.json"))
        .Serve("/api/persistent/matches", ResultsTestSupport.Fixture("matches.finalized.json"));

    /// <summary>The 26th, published but never finalized: it resolves, but serves no ranking.</summary>
    public static TugenyStub NotFinalized() => new TugenyStub()
        .Serve("/api/persistent/tournamentsBySlug/", ResultsTestSupport.Fixture("tournament-by-slug.not-finalized.json"))
        .Serve("/api/persistent/rankingsByTournamentId/", ResultsTestSupport.Fixture("rankings.not-finalized.json"))
        .Serve("/api/persistent/matches", ResultsTestSupport.Fixture("matches.not-finalized.json"));

    /// <summary>Tugeny's answer to an unknown slug: HTTP 200 with the body <c>null</c>.</summary>
    public static TugenyStub Unknown() => new TugenyStub()
        .Serve("/api/persistent/tournamentsBySlug/", ResultsTestSupport.Fixture("tournament-by-slug.unknown.json"));

    public static TugenyStub AlwaysFails(HttpStatusCode status = HttpStatusCode.InternalServerError) =>
        new() { _all = () => new HttpResponseMessage(status) };

    public static TugenyStub Hangs() => new() { _hang = true };

    /// <summary>Answer a path prefix with a JSON body.</summary>
    public TugenyStub Serve(string prefix, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((prefix, () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    /// <summary>Answer a path prefix with a body of <paramref name="bytes"/> bytes, sent without a length.</summary>
    public TugenyStub ServeBytes(string prefix, int bytes)
    {
        _routes.Add((prefix, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // A stream of unknown length, so the size guard must count rather than trust a header.
            Content = new StreamContent(new MemoryStream(Enumerable.Repeat((byte)' ', bytes).ToArray(), writable: false)),
        }));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (_hang)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }

        if (_all is not null)
        {
            return _all();
        }

        var path = request.RequestUri!.PathAndQuery;
        foreach (var (prefix, respond) in _routes.OrderByDescending(r => r.Prefix.Length))
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return respond();
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
