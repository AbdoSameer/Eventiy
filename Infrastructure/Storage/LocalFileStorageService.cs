using Domain.Abstractions.Storage;
using Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Storage;

internal sealed class LocalFileStorageService : IFileStorageService
{
    private const long MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _basePath;

    public LocalFileStorageService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<LocalFileStorageService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "events");
    }

    public async Task<Result<string>> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
            return Result<string>.Failure(
                Error.Validation("FileStorage.InvalidContentType",
                    $"Content type '{contentType}' is not allowed. Accepted types: {string.Join(", ", AllowedContentTypes)}."));

        if (fileStream.Length > MAX_FILE_SIZE)
            return Result<string>.Failure(
                Error.Validation("FileStorage.FileTooLarge",
                    $"File size exceeds the maximum allowed size of {MAX_FILE_SIZE / 1024 / 1024}MB."));

        try
        {
            var uniqueFileName = $"{Guid.NewGuid():N}_{fileName}";
            var relativePath = Path.Combine("uploads", "events", uniqueFileName);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await using var fileStreamWriter = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await fileStream.CopyToAsync(fileStreamWriter, ct);

            _logger.LogInformation("Uploaded file '{FileName}' to {Path}", fileName, relativePath);

            return Result<string>.Success(relativePath.Replace('\\', '/'));
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure(
                Error.Failure("FileStorage.UploadCancelled", "File upload was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file '{FileName}'", fileName);
            return Result<string>.Failure(
                Error.Failure("FileStorage.UploadFailed", $"Failed to upload file: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        try
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", storagePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found for deletion: {Path}", fullPath);
                return Result.Success();
            }

            await Task.Run(() => File.Delete(fullPath), ct);
            _logger.LogInformation("Deleted file: {Path}", fullPath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file '{Path}'", storagePath);
            return Result.Failure(
                Error.Failure("FileStorage.DeleteFailed", $"Failed to delete file: {ex.Message}"));
        }
    }

    public string GetPublicUrl(string storagePath)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return $"/{storagePath}";

        return $"{request.Scheme}://{request.Host}/{storagePath.Replace('\\', '/')}";
    }
}
