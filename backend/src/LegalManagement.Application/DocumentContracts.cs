namespace LegalManagement.Application;

public record StoredDocumentResult(string Provider, string Key, string FileName, long SizeBytes);
public interface IDocumentStorage
{
    Task<StoredDocumentResult> SaveAsync(Stream source, string extension, long maxBytes, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
public record CaseDocumentDto(Guid Id, string OriginalFileName, string Extension, string ContentType,
    long SizeBytes, string DocumentType, string? Notes, string UploadedBy, DateTimeOffset UploadedAt);
public class DocumentOptions
{
    public string Provider { get; set; } = "Local";
    public string RootPath { get; set; } = ".local/documents";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}
