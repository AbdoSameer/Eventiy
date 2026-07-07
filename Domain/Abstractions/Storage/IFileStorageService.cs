using Domain.Common;

namespace Domain.Abstractions.Storage;

public interface IFileStorageService
{
    Task<Result<string>> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(string storagePath, CancellationToken ct = default);

    string GetPublicUrl(string storagePath);
}
