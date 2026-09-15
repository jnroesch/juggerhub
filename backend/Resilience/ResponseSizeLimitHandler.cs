namespace JuggerHub.Resilience;

/// <summary>
/// Refuses an outbound response body larger than a limit, as a <b>permanent</b> failure (feature 050).
/// </summary>
/// <remarks>
/// <para>
/// Why not <see cref="HttpClient.MaxResponseContentBufferSize"/>: exceeding it throws
/// <see cref="HttpRequestException"/>, which the shared resilience pipeline treats as a transient
/// fault — so an oversized body would be fetched, and refused, once per attempt. An oversized body is
/// not going to shrink on a retry. This handler throws <see cref="ResponseTooLargeException"/>
/// instead, which the pipeline does not handle, so it fails once (Principle VII: "fail fast on
/// rejections").
/// </para>
/// <para>
/// Registered <b>after</b> <c>AddJuggerHubResilience</c>, which makes it the inner handler: it runs
/// once per attempt, and the body is read inside that attempt's time limit, so nothing waits
/// unbounded. It buffers at most <c>limit + 1</c> bytes before giving up. It is a size guard, not a
/// second resilience handler — no retry, timeout or breaker lives here.
/// </para>
/// </remarks>
public sealed class ResponseSizeLimitHandler : DelegatingHandler
{
    private readonly long _maxBytes;

    public ResponseSizeLimitHandler(long maxBytes) => _maxBytes = maxBytes;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content.Headers.ContentLength is { } declared && declared > _maxBytes)
        {
            response.Dispose();
            throw new ResponseTooLargeException(_maxBytes);
        }

        using var buffer = new MemoryStream();
        await using (var body = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            var chunk = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > _maxBytes)
                {
                    response.Dispose();
                    throw new ResponseTooLargeException(_maxBytes);
                }

                buffer.Write(chunk, 0, read);
            }
        }

        var content = new ByteArrayContent(buffer.ToArray());
        foreach (var header in response.Content.Headers)
        {
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content.Dispose();
        response.Content = content;
        return response;
    }
}

/// <summary>An outbound response body exceeded its limit. Deliberately not an <see cref="HttpRequestException"/>.</summary>
public sealed class ResponseTooLargeException : Exception
{
    public ResponseTooLargeException(long maxBytes)
        : base($"The response body exceeded the {maxBytes}-byte limit.")
    {
        MaxBytes = maxBytes;
    }

    public long MaxBytes { get; }
}
