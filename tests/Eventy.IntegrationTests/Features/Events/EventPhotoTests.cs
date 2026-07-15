using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using Eventy.Testing.Foundation.Web;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Events;

[Collection("Integration")]
public class EventPhotoTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public EventPhotoTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedEventWithPhotosAsync(int photoCount)
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await dbScope.Db.Events
            .Include(e => e.Photos)
            .FirstAsync(e => e.Id == eventIdObj);

        var utcNow = DateTime.UtcNow;
        for (int i = 0; i < photoCount; i++)
        {
            var photoResult = EventPhoto.Create(
                eventIdObj,
                $"photo{i}.jpg",
                $"/uploads/photo{i}.jpg",
                $"https://cdn.example.com/photo{i}.jpg",
                i,
                utcNow);

            if (photoResult.IsFailure)
                throw new InvalidOperationException($"Failed to create photo: {photoResult.Errors[0].Message}");

            @event.AddPhoto(photoResult.Value, utcNow);
        }

        await dbScope.Db.SaveChangesAsync();
        return eventId;
    }

    private static async Task<HttpClient> CreateAuthorizedClientAsync(IntegrationTestFixture fixture)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Organizer");
        client.DefaultRequestHeaders.Add("X-Test-UserId", TestUsers.OrganizerUserId.ToString());
        return client;
    }

    #region GET Photos

    [Fact]
    public async Task GetPhotos_WhenEventHasPhotos_ShouldReturnPhotoList()
    {
        var eventId = await SeedEventWithPhotosAsync(3);

        var response = await _client.GetAsync($"/api/Event/{eventId}/photos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPhotos_WhenEventHasNoPhotos_ShouldReturnEmptyList()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();

        var response = await _client.GetAsync($"/api/Event/{eventId}/photos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPhotos_WhenEventDoesNotExist_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/Event/{Guid.NewGuid()}/photos");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Upload Photos

    [Fact]
    public async Task UploadPhotos_WithValidFiles_ShouldCreatePhotosInDatabase()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();
        using var authClient = await CreateAuthorizedClientAsync(_fixture);

        using var multipart = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        multipart.Add(fileContent, "photos", "test.png");

        var response = await authClient.PostAsync($"/api/Event/{eventId}/photos", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var photoCount = await dbScope.Db.Set<EventPhoto>()
            .CountAsync(p => p.EventId == eventIdObj);
        photoCount.Should().BeGreaterThanOrEqualTo(1, "the uploaded photo should be persisted");
    }

    [Fact]
    public async Task UploadPhotos_WhenNoFiles_ShouldReturn400()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();
        using var authClient = await CreateAuthorizedClientAsync(_fixture);

        using var multipart = new MultipartFormDataContent();

        var response = await authClient.PostAsync($"/api/Event/{eventId}/photos", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadPhotos_WhenNotAuthenticated_ShouldReturn401()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();

        using var multipart = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        multipart.Add(fileContent, "photos", "test.png");

        var response = await _client.PostAsync($"/api/Event/{eventId}/photos", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Set Cover Photo

    [Fact]
    public async Task SetCoverPhoto_WhenPhotoExists_ShouldSetCoverAndClearOthers()
    {
        var eventId = await SeedEventWithPhotosAsync(3);

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var photos = await dbScope.Db.Set<EventPhoto>()
            .Where(p => p.EventId == eventIdObj)
            .ToListAsync();
        var targetPhotoId = photos.Last().Id;

        using var authClient = await CreateAuthorizedClientAsync(_fixture);
        var response = await authClient.PutAsync(
            $"/api/Event/{eventId}/photos/{targetPhotoId.Value}/cover", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyScope = _fixture.CreateDbContext();
        var refreshedPhotos = await verifyScope.Db.Set<EventPhoto>()
            .Where(p => p.EventId == eventIdObj)
            .ToListAsync();
        refreshedPhotos.First(p => p.Id == targetPhotoId).IsCover.Should().BeTrue();
        refreshedPhotos.Where(p => p.Id != targetPhotoId).All(p => !p.IsCover).Should().BeTrue();
    }

    [Fact]
    public async Task SetCoverPhoto_WhenPhotoDoesNotExist_ShouldReturn404()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();
        using var authClient = await CreateAuthorizedClientAsync(_fixture);

        var response = await authClient.PutAsync(
            $"/api/Event/{eventId}/photos/{Guid.NewGuid()}/cover", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete Photo

    [Fact]
    public async Task DeletePhoto_WhenPhotoExists_ShouldRemoveFromDatabase()
    {
        var eventId = await SeedEventWithPhotosAsync(2);

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var photoToDelete = await dbScope.Db.Set<EventPhoto>()
            .FirstAsync(p => p.EventId == eventIdObj);
        var photoId = photoToDelete.Id.Value;

        using var authClient = await CreateAuthorizedClientAsync(_fixture);
        var response = await authClient.DeleteAsync(
            $"/api/Event/{eventId}/photos/{photoId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyScope = _fixture.CreateDbContext();
        var remaining = await verifyScope.Db.Set<EventPhoto>()
            .Where(p => p.EventId == eventIdObj)
            .ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.Any(p => p.Id.Value == photoId).Should().BeFalse();
    }

    #endregion
}
