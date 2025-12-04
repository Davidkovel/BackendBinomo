namespace BinomoBackend.Domain.Interfaces;


public interface IFileStorage
{
    Task<string> SaveReceiptAsync(
        Stream fileStream, 
        string fileName, 
        string contentType,
        CancellationToken ct = default);
}