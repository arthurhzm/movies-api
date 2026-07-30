using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoviesAPI.Controllers;
using MoviesAPI.Data;
using MoviesAPI.Services;

namespace MoviesAPI.Tests;

internal static class TestSupport
{
    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"movies-api-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    public static LetterboxdService CreateService(
        AppDbContext context,
        HttpResponseMessage? response = null)
    {
        response ??= new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<rss><channel /></rss>", Encoding.UTF8, "application/xml")
        };

        return new LetterboxdService(
            context,
            new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(response))),
            NullLogger<LetterboxdService>.Instance,
            new LetterboxdCsvParser(),
            new NoopTmdbResolver());
    }

    public static LetterboxdController CreateController(
        AppDbContext context,
        int authenticatedUserId = 1,
        HttpResponseMessage? response = null)
    {
        var controller = new LetterboxdController(context, CreateService(context, response));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, authenticatedUserId.ToString())],
                    "TestAuthentication"))
            }
        };

        return controller;
    }

    public static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));

    public static IFormFile CsvFile(string csv)
    {
        var stream = CsvStream(csv);
        return new FormFile(stream, 0, stream.Length, "file", "ratings.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
