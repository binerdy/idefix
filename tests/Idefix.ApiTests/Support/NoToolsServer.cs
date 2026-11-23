using System.Net;
using System.Text;
using System.Text.Json;

namespace Idefix.ApiTests.Support;

internal sealed class NoToolsServer(string answer) : HttpMessageHandler
{
    public int Rejected { get; private set; }

    public int Answered { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (body.RootElement.TryGetProperty("tools", out _))
        {
            this.Rejected++;
            return Reply(HttpStatusCode.BadRequest, "application/json", """{"error":{"message":"model does not support tools","type":"invalid_request_error"}}""");
        }

        this.Answered++;
        var chunk = $$$"""{"id":"c","object":"chat.completion.chunk","created":1,"model":"small","choices":[{"index":0,"delta":{"role":"assistant","content":{{{JsonSerializer.Serialize(answer)}}}},"finish_reason":"stop"}]}""";
        return Reply(HttpStatusCode.OK, "text/event-stream", $"data: {chunk}\n\ndata: [DONE]\n\n");
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string mediaType, string content) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, mediaType) };
}
