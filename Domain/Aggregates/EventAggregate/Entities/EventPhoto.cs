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
            return Result<EventPhoto>.Failure(EventPhotoErrors.FileNameCannotBeEmpty());

        if (fileName.Length > MAX_FILE_NAME_LENGTH)
            return Result<EventPhoto>.Failure(EventPhotoErrors.FileNameTooLong(MAX_FILE_NAME_LENGTH));

        if (string.IsNullOrWhiteSpace(storagePath))
            return Result<EventPhoto>.Failure(EventPhotoErrors.StoragePathCannotBeEmpty());

        if (storagePath.Length > MAX_STORAGE_PATH_LENGTH)
            return Result<EventPhoto>.Failure(EventPhotoErrors.StoragePathTooLong(MAX_STORAGE_PATH_LENGTH));

        if (string.IsNullOrWhiteSpace(publicUrl))
            return Result<EventPhoto>.Failure(EventPhotoErrors.PublicUrlCannotBeEmpty());

        if (publicUrl.Length > MAX_PUBLIC_URL_LENGTH)
            return Result<EventPhoto>.Failure(EventPhotoErrors.PublicUrlTooLong(MAX_PUBLIC_URL_LENGTH));

        if (displayOrder < 0)
            return Result<EventPhoto>.Failure(EventPhotoErrors.InvalidDisplayOrder());

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
            return Result.Failure(EventPhotoErrors.AlreadyCover());

        IsCover = true;
        return Result.Success();
    }

    public Result RemoveCover()
    {
        if (!IsCover)
            return Result.Failure(EventPhotoErrors.NotCover());

        IsCover = false;
        return Result.Success();
    }

    public Result UpdateCaption(string? caption)
    {
        if (caption?.Length > MAX_CAPTION_LENGTH)
            return Result.Failure(EventPhotoErrors.CaptionTooLong(MAX_CAPTION_LENGTH));

        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        return Result.Success();
    }

    public Result UpdateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            return Result.Failure(EventPhotoErrors.InvalidDisplayOrder());

        DisplayOrder = displayOrder;
        return Result.Success();
    }
}
