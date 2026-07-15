using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates.EventAggregate;

public class EventPhotoManagementTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Address DefaultAddress = Address.Create(
        "Egypt", "Cairo", "Nile City", "11511", latitude: 30.0444, longitude: 31.2357).Value;

    private static Event CreateValidEvent()
    {
        return Event.Create("Photo Test Event", 100, UtcNow.AddDays(30),
            DefaultAddress, "Description", EventType.Music, UtcNow).Value;
    }

    private static EventPhoto CreateValidPhoto(int displayOrder = 0, string fileName = "photo.jpg")
    {
        return EventPhoto.Create(
            EventId.FromDatabase(Guid.NewGuid()),
            fileName,
            $"/uploads/{fileName}",
            $"https://cdn.example.com/{fileName}",
            displayOrder,
            UtcNow).Value;
    }

    #region AddPhoto

    [Fact]
    public void AddPhoto_WhenEventIsEmpty_ShouldAddAndAutoSetCover()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();

        var result = @event.AddPhoto(photo, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.Should().HaveCount(1);
        @event.Photos.First().IsCover.Should().BeTrue();
    }

    [Fact]
    public void AddPhoto_SecondPhoto_ShouldNotAutoSetCover()
    {
        var @event = CreateValidEvent();
        var photo1 = CreateValidPhoto();
        var photo2 = CreateValidPhoto();

        @event.AddPhoto(photo1, UtcNow);
        var result = @event.AddPhoto(photo2, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.Should().HaveCount(2);
        @event.Photos.First().IsCover.Should().BeTrue();
        @event.Photos.Last().IsCover.Should().BeFalse();
    }

    [Fact]
    public void AddPhoto_WhenMaxPhotosReached_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        for (int i = 0; i < 10; i++)
        {
            @event.AddPhoto(CreateValidPhoto(fileName: $"photo{i}.jpg"), UtcNow);
        }

        var result = @event.AddPhoto(CreateValidPhoto(fileName: "overflow.jpg"), UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.MaxPhotosReached");
    }

    [Fact]
    public void AddPhoto_WithNullPhoto_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();

        var result = @event.AddPhoto(null!, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddPhoto_ShouldRaiseDomainEvent()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.ClearDomainEvents();

        @event.AddPhoto(photo, UtcNow);

        @event.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "EventPhotosUpdatedEvent");
    }

    #endregion

    #region RemovePhoto

    [Fact]
    public void RemovePhoto_WhenPhotoExists_ShouldRemovePhoto()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.AddPhoto(photo, UtcNow);

        var result = @event.RemovePhoto(photo.Id, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.Should().BeEmpty();
    }

    [Fact]
    public void RemovePhoto_WhenPhotoDoesNotExist_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var fakeId = EventPhotoId.Create(Guid.NewGuid()).Value;

        var result = @event.RemovePhoto(fakeId, UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.PhotoNotFound");
    }

    [Fact]
    public void RemovePhoto_ShouldRaiseDomainEvent()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.AddPhoto(photo, UtcNow);
        @event.ClearDomainEvents();

        @event.RemovePhoto(photo.Id, UtcNow);

        @event.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "EventPhotosUpdatedEvent");
    }

    #endregion

    #region SetCoverPhoto

    [Fact]
    public void SetCoverPhoto_WhenPhotoExists_ShouldSetCoverAndClearOthers()
    {
        var @event = CreateValidEvent();
        var photo1 = CreateValidPhoto();
        var photo2 = CreateValidPhoto();
        @event.AddPhoto(photo1, UtcNow);
        @event.AddPhoto(photo2, UtcNow);

        var result = @event.SetCoverPhoto(photo2.Id, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.First(p => p.Id == photo2.Id).IsCover.Should().BeTrue();
        @event.Photos.First(p => p.Id == photo1.Id).IsCover.Should().BeFalse();
    }

    [Fact]
    public void SetCoverPhoto_WhenPhotoDoesNotExist_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var fakeId = EventPhotoId.Create(Guid.NewGuid()).Value;

        var result = @event.SetCoverPhoto(fakeId, UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.PhotoNotFound");
    }

    [Fact]
    public void SetCoverPhoto_WhenAlreadyCover_ShouldSucceedWithoutChange()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.AddPhoto(photo, UtcNow);

        var result = @event.SetCoverPhoto(photo.Id, UtcNow);

        result.IsSuccess.Should().BeTrue();
        photo.IsCover.Should().BeTrue();
    }

    #endregion

    #region UpdatePhotoCaption

    [Fact]
    public void UpdatePhotoCaption_WhenPhotoExists_ShouldUpdateCaption()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.AddPhoto(photo, UtcNow);

        var result = @event.UpdatePhotoCaption(photo.Id, "New caption", UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.First(p => p.Id == photo.Id).Caption.Should().Be("New caption");
    }

    [Fact]
    public void UpdatePhotoCaption_WhenPhotoDoesNotExist_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var fakeId = EventPhotoId.Create(Guid.NewGuid()).Value;

        var result = @event.UpdatePhotoCaption(fakeId, "caption", UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.PhotoNotFound");
    }

    #endregion

    #region ReorderPhotos

    [Fact]
    public void ReorderPhotos_WithValidOrder_ShouldUpdateDisplayOrders()
    {
        var @event = CreateValidEvent();
        var photo1 = CreateValidPhoto(0, "p1.jpg");
        var photo2 = CreateValidPhoto(1, "p2.jpg");
        var photo3 = CreateValidPhoto(2, "p3.jpg");
        @event.AddPhoto(photo1, UtcNow);
        @event.AddPhoto(photo2, UtcNow);
        @event.AddPhoto(photo3, UtcNow);

        var newOrder = new List<EventPhotoId> { photo3.Id, photo1.Id, photo2.Id };
        var result = @event.ReorderPhotos(newOrder, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Photos.First(p => p.Id == photo3.Id).DisplayOrder.Should().Be(0);
        @event.Photos.First(p => p.Id == photo1.Id).DisplayOrder.Should().Be(1);
        @event.Photos.First(p => p.Id == photo2.Id).DisplayOrder.Should().Be(2);
    }

    [Fact]
    public void ReorderPhotos_WithMismatchedCount_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var photo1 = CreateValidPhoto(0, "p1.jpg");
        var photo2 = CreateValidPhoto(1, "p2.jpg");
        @event.AddPhoto(photo1, UtcNow);
        @event.AddPhoto(photo2, UtcNow);

        var newOrder = new List<EventPhotoId> { photo1.Id };
        var result = @event.ReorderPhotos(newOrder, UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.InvalidPhotoOrder");
    }

    [Fact]
    public void ReorderPhotos_WithInvalidPhotoId_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var photo1 = CreateValidPhoto(0, "p1.jpg");
        @event.AddPhoto(photo1, UtcNow);

        var fakeId = EventPhotoId.Create(Guid.NewGuid()).Value;
        var newOrder = new List<EventPhotoId> { fakeId };
        var result = @event.ReorderPhotos(newOrder, UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Event.InvalidPhotoOrder");
    }

    [Fact]
    public void ReorderPhotos_WithNullList_ShouldReturnFailure()
    {
        var @event = CreateValidEvent();
        var photo = CreateValidPhoto();
        @event.AddPhoto(photo, UtcNow);

        var result = @event.ReorderPhotos(null!, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
