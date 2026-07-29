using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MoviesAPI.Services;

namespace MoviesAPI.Tests;

public sealed class HuggingFaceServiceTests
{
    [Fact]
    public async Task GenerateStructuredJsonAsync_UsesRecommendationModelAndJsonSchema()
    {
        var handler = new RecordingHandler("""{"choices":[{"message":{"content":"[]"}}]}""");
        var service = CreateService(handler);

        var result = await service.GenerateStructuredJsonAsync(
            "Sugira filmes.",
            "recommendations",
            new { type = "array" });

        Assert.Equal("[]", result);
        Assert.Contains("Qwen/Qwen3-32B", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("json_schema", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal("Bearer hf-test-token", handler.Authorization);
    }

    [Fact]
    public async Task GenerateConversationAsync_UsesConversationModelWithoutStructuredFormat()
    {
        var handler = new RecordingHandler("""{"choices":[{"message":{"content":"<p>Olá!</p>"}}]}""");
        var service = CreateService(handler);

        var result = await service.GenerateConversationAsync("Olá");

        Assert.Equal("<p>Olá!</p>", result);
        Assert.Contains("openai/gpt-oss-120b", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("response_format", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateConversationAsync_ProviderErrorDoesNotExposeResponseBody()
    {
        var handler = new RecordingHandler("detalhe interno", HttpStatusCode.TooManyRequests);
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<HuggingFaceGenerationException>(
            () => service.GenerateConversationAsync("Olá"));

        Assert.DoesNotContain("detalhe interno", exception.Message, StringComparison.Ordinal);
    }

    private static HuggingFaceService CreateService(RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HuggingFace:Token"] = "hf-test-token"
            })
            .Build();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://router.huggingface.co/v1/")
        };

        return new HuggingFaceService(
            new StubHttpClientFactory(client),
            configuration,
            NullLogger<HuggingFaceService>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
