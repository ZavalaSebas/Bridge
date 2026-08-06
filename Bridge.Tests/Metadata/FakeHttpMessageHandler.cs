namespace Bridge.Tests.Metadata;

/// <summary>Lets IGDB tests exercise the real HttpClient/request-building code without hitting the real network or needing real IGDB credentials — the responder inspects the outgoing request and returns a canned response.</summary>
internal class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(responder(request));
    }
}
