using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;

namespace Domain.Aggregates.EventAggregate.Entities;

public sealed class EventPhoto : Entity<EventPhotoId>
{
    private const int MAX_FILE_NAME_LENGTH = 255;
    private const int MAX_CAPTION_LENGTH = 500;
    private const int MAX_STORAGE_PATH_LENGTH = 1000;
    private const int MAX_PUBLIC_URL_LENGTH = 1000;

    public EventId EventId { get; private set; } = null!;
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string PublicUrl { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsCover { get; private set; }
    public DateTime UploadedAt { get; private set; }

    protected EventPhoto() : base(default!) { }

    private EventPhoto(
        EventPhotoId id,
        EventId eventId,
        string fileName,
        string storagePath,
        string publicUrl,
        int displayOrder,
        DateTime uploadedAt) : base(id)
    {
        EventId = eventId;
        FileName = fileName;
        StoragePath = storagePath;
        PublicUrl = publicUrl;
        DisplayOrder = displayOrder;
        IsCover = false;
        UploadedAt = uploadedAt;
    }

    public static Result<EventPhoto> Create(
        EventId eventId,
        string fileName,
        string storagePath,
        string publicUrl,
        int displayOrder,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.FileNameEmpty", "File name cannot be empty."));

        if (fileName.Length > MAX_FILE_NAME_LENGTH)
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.FileNameTooLong",
                    $"File name cannot exceed {MAX_FILE_NAME_LENGTH} characters."));

        if (string.IsNullOrWhiteSpace(storagePath))
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.StoragePathEmpty", "Storage path cannot be empty."));

        if (storagePath.Length > MAX_STORAGE_PATH_LENGTH)
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.StoragePathTooLong",
                    $"Storage path cannot exceed {MAX_STORAGE_PATH_LENGTH} characters."));

        if (string.IsNullOrWhiteSpace(publicUrl))
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.PublicUrlEmpty", "Public URL cannot be empty."));

        if (publicUrl.Length > MAX_PUBLIC_URL_LENGTH)
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.PublicUrlTooLong",
                    $"Public URL cannot exceed {MAX_PUBLIC_URL_LENGTH} characters."));

        if (displayOrder < 0)
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.InvalidDisplayOrder", "Display order cannot be negative."));

        var idResult = EventPhotoId.Create(Guid.NewGuid());
        if (idResult.IsFailure)
            return Result<EventPhoto>.Failure(idResult.Errors.ToArray());

        return Result<EventPhoto>.Success(new EventPhoto(
            idResult.Value, eventId, fileName, storagePath, publicUrl,
            displayOrder, utcNow));
    }

    public Result SetCover()
    {
        if (IsCover)
            return Result.Failure(
                Error.Conflict("EventPhoto.AlreadyCover", "This photo is already the cover."));

        IsCover = true;
        return Result.Success();
    }

    public Result RemoveCover()
    {
        if (!IsCover)
            return Result.Failure(
                Error.Conflict("EventPhoto.NotCover", "This photo is not the cover."));

        IsCover = false;
        return Result.Success();
    }

    public Result UpdateCaption(string? caption)
    {
        if (caption?.Length > MAX_CAPTION_LENGTH)
            return Result<EventPhoto>.Failure(
                Error.Validation("EventPhoto.CaptionTooLong",
                    $"Caption cannot exceed {MAX_CAPTION_LENGTH} characters."));

        Caption = caption?.Trim();
        return Result.Success();
    }

    public Result UpdateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            return Result.Failure(
                Error.Validation("EventPhoto.InvalidDisplayOrder", "Display order cannot be negative."));

        DisplayOrder = displayOrder;
        return Result.Success();
    }
}
