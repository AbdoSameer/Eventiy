using FluentValidation;

namespace Application.Features.Events.Commands.UploadEventPhotos;

internal sealed class UploadEventPhotosCommandValidator : AbstractValidator<UploadEventPhotosCommand>
{
    private const int MaxFilesPerUpload = 10;
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public UploadEventPhotosCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("Event ID is required.");

        RuleFor(x => x.Photos)
            .NotEmpty().WithMessage("At least one photo is required.");

        RuleFor(x => x.Photos)
            .Must(files => files.Count <= MaxFilesPerUpload)
                .WithMessage($"Cannot upload more than {MaxFilesPerUpload} photos at once.");

        RuleForEach(x => x.Photos)
            .Must(BeValidFile).WithMessage($"Each file must be a JPEG, PNG, or WebP image under {MaxFileSize / 1024 / 1024}MB.");
    }

    private static bool BeValidFile(FileUploadData file)
    {
        if (file == null || file.Length == 0)
            return false;

        if (file.Length > MaxFileSize)
            return false;

        return AllowedContentTypes.Contains(file.ContentType);
    }
}
