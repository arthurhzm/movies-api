using Microsoft.EntityFrameworkCore;
using MoviesAPI.Models;

namespace MoviesAPI.Tests;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void UserMovieFeedback_ConfiguresLetterboxdUriConstraintAndUniquePartialIndex()
    {
        using var context = TestSupport.CreateContext();
        var entity = context.Model.FindEntityType(typeof(UserMovieFeedbackModel));
        Assert.NotNull(entity);

        var uriProperty = entity.FindProperty(nameof(UserMovieFeedbackModel.LetterboxdUri));
        Assert.NotNull(uriProperty);
        Assert.Equal(UserMovieFeedbackModel.MaxLetterboxdUriLength, uriProperty.GetMaxLength());

        var index = Assert.Single(entity.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(UserMovieFeedbackModel.UserId), nameof(UserMovieFeedbackModel.LetterboxdUri)]));
        Assert.True(index.IsUnique);
        Assert.Equal("\"LetterboxdUri\" IS NOT NULL", index.GetFilter());
    }
}
