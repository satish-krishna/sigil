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
            // Synchronous delay is fine for tests against Polly's FakeTimeProvider or
            // when the test explicitly cancels. Avoid in tests that exceed CI budgets.
            Thread.Sleep(delay);
            var response = new HttpResponseMessage(finalStatus);
            if (body is not null)
                response.Content = new StringContent(body);
            return response;
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Capture a snapshot. Cloning the body is awkward; tests inspect the
        // original request object so we just record the reference.
        Requests.Add(request);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);

        if (_scripted.Count == 0)
            throw new InvalidOperationException(
                $"FakeHttpMessageHandler received an unexpected request to {request.RequestUri}; " +
                "queue an EnqueueResponse / EnqueueException for every expected call.");

        var produce = _scripted.Dequeue();
        return Task.FromResult(produce(request));
    }
}
