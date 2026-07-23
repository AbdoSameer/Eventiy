using Domain.Common;

namespace Domain.Errors;

public static class EventPhotoErrors
{
    // ===== Validation Errors (400 Bad Request) 

    public static Error EventIdRequired()
        => Error.Validation(
            "EventPhoto.EventIdRequired",
            "Event ID is required.");

    public static Error FileNameCannotBeEmpty()
        => Error.Validation(
            "EventPhoto.FileNameCannotBeEmpty",
            "File name cannot be empty.");

    public static Error FileNameTooLong(int maxLength)
        => Error.Validation(
            "EventPhoto.FileNameTooLong",
            $"File name cannot exceed {maxLength} characters.");

    public static Error StoragePathCannotBeEmpty()
        => Error.Validation(
            "EventPhoto.StoragePathCannotBeEmpty",
            "Storage path cannot be empty.");

    public static Error StoragePathTooLong(int maxLength)
        => Error.Validation(
            "EventPhoto.StoragePathTooLong",
            $"Storage path cannot exceed {maxLength} characters.");

    public static Error PublicUrlCannotBeEmpty()
        => Error.Validation(
            "EventPhoto.PublicUrlCannotBeEmpty",
            "Public URL cannot be empty.");

    public static Error PublicUrlTooLong(int maxLength)
        => Error.Validation(
            "EventPhoto.PublicUrlTooLong",
            $"Public URL cannot exceed {maxLength} characters.");

    public static Error CaptionTooLong(int maxLength)
        => Error.Validation(
            "EventPhoto.CaptionTooLong",
            $"Caption cannot exceed {maxLength} characters.");

    public static Error InvalidDisplayOrder()
        => Error.Validation(
            "EventPhoto.InvalidDisplayOrder",
            "Display order cannot be negative.");

    // ===== Conflict / State Errors (409 Conflict) 

    public static Error AlreadyCover()
        => Error.Conflict(
            "EventPhoto.AlreadyCover",
            "This photo is already the cover.");

    public static Error NotCover()
        => Error.Conflict(
            "EventPhoto.NotCover",
            "This photo is not the cover.");

    // ===== Not Found Errors (404 Not Found) 

    public static Error PhotoNotFound(Guid photoId)
        => Error.NotFound(
            "EventPhoto.NotFound",
            $"Photo with ID {photoId} was not found.");
}
