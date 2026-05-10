using System.Net;

namespace Sigil.Infrastructure.Tests.Gateway;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _scripted = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void EnqueueResponse(HttpStatusCode status, string? body = null, string mediaType = "application/json")
        => _scripted.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
            return response;
        });

    public void EnqueueException(Exception exception)
        => _scripted.Enqueue(_ => throw exception);

    public void EnqueueDelay(TimeSpan delay, HttpStatusCode finalStatus = HttpStatusCode.OK, string? body = null)
        => _scripted.Enqueue(req =>
        {
            // Thread.Sleep blocks the worker thread for the full delay and does NOT
            // honour the SendAsync CancellationToken once entered. Use only with
            // pre-cancelled tokens (caught at the top of SendAsync) or tests where
            // the delay is short enough not to matter on CI.
            Thread.Sleep(delay);
            var response = new HttpResponseMessage(finalStatus);
            if (body is not null)
                response.Content = new StringContent(body);
            return response;
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        // Buffer the body before the gateway disposes the request, then detach the
        // original content from the outgoing request so disposal does not cascade to
        // our snapshot. Tests read Content from the captured snapshot message.
        HttpContent? snapshot = null;
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var contentType = request.Content.Headers.ContentType;
            var buffered = new ByteArrayContent(bytes);
            if (contentType is not null)
                buffered.Headers.ContentType = contentType;
            snapshot = buffered;
            // Null the content on the live request so disposing the HttpRequestMessage
            // (via `using var request` in DispatchAsync) does not dispose our snapshot.
            request.Content = null;
        }

        // Build a lightweight snapshot record that tests can inspect freely.
        var captured = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = snapshot
        };
        foreach (var header in request.Headers)
            captured.Headers.TryAddWithoutValidation(header.Key, header.Value);

        Requests.Add(captured);

        if (_scripted.Count == 0)
            throw new InvalidOperationException(
                $"FakeHttpMessageHandler received an unexpected request to {request.RequestUri}; " +
                "queue an EnqueueResponse / EnqueueException for every expected call.");

        var produce = _scripted.Dequeue();
        return produce(request);
    }
}
