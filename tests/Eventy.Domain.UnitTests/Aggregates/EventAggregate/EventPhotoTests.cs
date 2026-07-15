using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates.EventAggregate;

public class EventPhotoTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly EventId DefaultEventId = EventId.FromDatabase(Guid.NewGuid());

    private static Result<EventPhoto> CreateValidPhoto(
        string fileName = "concert.jpg",
        string storagePath = "/uploads/concert.jpg",
        string publicUrl = "https://cdn.example.com/concert.jpg",
        int displayOrder = 0)
    {
        return EventPhoto.Create(DefaultEventId, fileName, storagePath, publicUrl, displayOrder, UtcNow);
    }

    #region Create

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        var result = CreateValidPhoto();

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be(DefaultEventId);
        result.Value.FileName.Should().Be("concert.jpg");
        result.Value.StoragePath.Should().Be("/uploads/concert.jpg");
        result.Value.PublicUrl.Should().Be("https://cdn.example.com/concert.jpg");
        result.Value.DisplayOrder.Should().Be(0);
        result.Value.IsCover.Should().BeFalse();
        result.Value.UploadedAt.Should().Be(UtcNow);
    }

    [Fact]
    public void Create_WithEmptyFileName_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(fileName: "");

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.FileNameCannotBeEmpty");
    }

    [Fact]
    public void Create_WithWhitespaceFileName_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(fileName: "   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithFileNameTooLong_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(fileName: new string('a', 256));

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.FileNameTooLong");
    }

    [Fact]
    public void Create_WithEmptyStoragePath_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(storagePath: "");

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.StoragePathCannotBeEmpty");
    }

    [Fact]
    public void Create_WithStoragePathTooLong_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(storagePath: new string('a', 1001));

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.StoragePathTooLong");
    }

    [Fact]
    public void Create_WithEmptyPublicUrl_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(publicUrl: "");

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.PublicUrlCannotBeEmpty");
    }

    [Fact]
    public void Create_WithPublicUrlTooLong_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(publicUrl: new string('a', 1001));

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.PublicUrlTooLong");
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_ShouldReturnFailure()
    {
        var result = CreateValidPhoto(displayOrder: -1);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.InvalidDisplayOrder");
    }

    #endregion

    #region SetCover

    [Fact]
    public void SetCover_WhenNotCover_ShouldSetIsCoverTrue()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.SetCover();

        result.IsSuccess.Should().BeTrue();
        photo.IsCover.Should().BeTrue();
    }

    [Fact]
    public void SetCover_WhenAlreadyCover_ShouldReturnFailure()
    {
        var photo = CreateValidPhoto().Value;
        photo.SetCover();

        var result = photo.SetCover();

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.AlreadyCover");
    }

    #endregion

    #region RemoveCover

    [Fact]
    public void RemoveCover_WhenIsCover_ShouldSetIsCoverFalse()
    {
        var photo = CreateValidPhoto().Value;
        photo.SetCover();

        var result = photo.RemoveCover();

        result.IsSuccess.Should().BeTrue();
        photo.IsCover.Should().BeFalse();
    }

    [Fact]
    public void RemoveCover_WhenNotCover_ShouldReturnFailure()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.RemoveCover();

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.NotCover");
    }

    #endregion

    #region UpdateCaption

    [Fact]
    public void UpdateCaption_WithValidCaption_ShouldUpdateCaption()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateCaption("Main stage photo");

        result.IsSuccess.Should().BeTrue();
        photo.Caption.Should().Be("Main stage photo");
    }

    [Fact]
    public void UpdateCaption_WithNull_ShouldSetCaptionNull()
    {
        var photo = CreateValidPhoto().Value;
        photo.UpdateCaption("Some caption");

        var result = photo.UpdateCaption(null);

        result.IsSuccess.Should().BeTrue();
        photo.Caption.Should().BeNull();
    }

    [Fact]
    public void UpdateCaption_WithWhitespace_ShouldTrimToNull()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateCaption("   ");

        result.IsSuccess.Should().BeTrue();
        photo.Caption.Should().BeNull();
    }

    [Fact]
    public void UpdateCaption_WithTooLongCaption_ShouldReturnFailure()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateCaption(new string('a', 501));

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.CaptionTooLong");
    }

    [Fact]
    public void UpdateCaption_WithExactlyMaxLength_ShouldSucceed()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateCaption(new string('a', 500));

        result.IsSuccess.Should().BeTrue();
        photo.Caption.Should().HaveLength(500);
    }

    #endregion

    #region UpdateDisplayOrder

    [Fact]
    public void UpdateDisplayOrder_WithValidValue_ShouldUpdateDisplayOrder()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateDisplayOrder(5);

        result.IsSuccess.Should().BeTrue();
        photo.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void UpdateDisplayOrder_WithZero_ShouldSucceed()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateDisplayOrder(0);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdateDisplayOrder_WithNegativeValue_ShouldReturnFailure()
    {
        var photo = CreateValidPhoto().Value;

        var result = photo.UpdateDisplayOrder(-1);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("EventPhoto.InvalidDisplayOrder");
    }

    #endregion

    #region SetCover + RemoveCover Round-Trip

    [Fact]
    public void SetCoverThenRemoveCover_ThenSetCoverAgain_ShouldSucceed()
    {
        var photo = CreateValidPhoto().Value;

        photo.SetCover().IsSuccess.Should().BeTrue();
        photo.RemoveCover().IsSuccess.Should().BeTrue();
        var result = photo.SetCover();

        result.IsSuccess.Should().BeTrue();
        photo.IsCover.Should().BeTrue();
    }

    #endregion
}
