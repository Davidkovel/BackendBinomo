using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Infrastructure.Services;

public class LocalFileStorageService : IFileStorage
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _uploadPath;

    public LocalFileStorageService(
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _uploadPath = configuration["FileStorage:UploadPath"] ?? "uploads/receipts";
        
        // Создаём директорию если не существует
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<string> SaveReceiptAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            // Генерируем безопасное имя файла
            var safeFileName = $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}";
            var fullPath = Path.Combine(_uploadPath, safeFileName);

            await using var fileStreamWriter = new FileStream(fullPath, FileMode.Create);
            await fileStream.CopyToAsync(fileStreamWriter, ct);

            _logger.LogInformation("✅ File saved: {FileName}", safeFileName);

            // Возвращаем относительный путь
            return $"/uploads/receipts/{safeFileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to save file: {FileName}", fileName);
            throw;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}