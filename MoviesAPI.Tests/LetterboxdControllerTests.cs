using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Controllers;
using MoviesAPI.Models;

namespace MoviesAPI.Tests;

public sealed class LetterboxdControllerTests
{
    [Fact]
    public async Task ImportCsv_MissingFile_ReturnsBadRequest()
    {
        await using var context = TestSupport.CreateContext();
        var controller = TestSupport.CreateController(context);

        var result = await controller.ImportCsv(1, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportCsv_DifferentAuthenticatedUser_ReturnsForbiddenAndPersistsNothing()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var controller = TestSupport.CreateController(context, authenticatedUserId: 2);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.5\n";

        var result = await controller.ImportCsv(
            1,
            TestSupport.CsvFile(csv),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(context.UserMovieFeedback);
    }

    [Fact]
    public async Task ImportCsv_FileLargerThanTenMegabytes_ReturnsPayloadTooLarge()
    {
        await using var context = TestSupport.CreateContext();
        var controller = TestSupport.CreateController(context);
        var file = new FormFile(Stream.Null, 0, (10 * 1024 * 1024) + 1, "file", "ratings.csv")
        {
            Headers = new HeaderDictionary()
        };

        var result = await controller.ImportCsv(1, file, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
    }

    [Fact]
    public void ImportCsv_ConfiguresMultipartOverheadSeparatelyFromFileLimit()
    {
        var method = typeof(LetterboxdController).GetMethod(nameof(LetterboxdController.ImportCsv));
        Assert.NotNull(method);

        var requestLimit = Assert.Single(
            method.CustomAttributes,
            attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));
        var formLimit = Assert.Single(
            method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(
            11L * 1024 * 1024,
            Assert.IsType<long>(requestLimit.ConstructorArguments.Single().Value));
        Assert.Equal(11L * 1024 * 1024, formLimit.MultipartBodyLengthLimit);
    }

    [Fact]
    public async Task ImportCsv_InvalidCsv_ReturnsUnprocessableEntityAndPersistsNothing()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var controller = TestSupport.CreateController(context);
        const string invalidCsv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.25\n";

        var result = await controller.ImportCsv(
            1,
            TestSupport.CsvFile(invalidCsv),
            CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Empty(context.UserMovieFeedback);
    }

    [Fact]
    public async Task ImportCsv_ValidCsv_ReturnsOkAndImportsHistory()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var controller = TestSupport.CreateController(context);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.5\n";

        var result = await controller.ImportCsv(
            1,
            TestSupport.CsvFile(csv),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var feedback = Assert.Single(context.UserMovieFeedback);
        Assert.Equal(4.5, feedback.Rating);
        Assert.Equal("https://boxd.it/arrival", feedback.LetterboxdUri);
    }

    [Fact]
    public async Task Connect_UpstreamFailureDoesNotPersistUsername()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var controller = TestSupport.CreateController(
            context,
            response: new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await controller.Connect(
            1,
            new ConnectLetterboxdRequest("missing_user"),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        context.ChangeTracker.Clear();
        Assert.Null((await context.Auth.SingleAsync()).LetterboxdUsername);
    }
}
