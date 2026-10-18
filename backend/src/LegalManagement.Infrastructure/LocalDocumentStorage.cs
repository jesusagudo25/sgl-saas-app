using LegalManagement.Application;

namespace LegalManagement.Infrastructure;

public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly string root;
    public LocalDocumentStorage(string rootPath, string contentRoot)
    {
        root = Path.GetFullPath(rootPath, contentRoot);
        var webRoot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot"));
        if (root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) || root.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document storage must be outside wwwroot.");
    }
    private string Resolve(string key)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"\A[a-f0-9]{32}\.[a-z0-9]{1,10}\z"))
            throw new ArgumentException("Invalid storage key.");
        return Path.Combine(root, key);
    }
    public async Task<StoredDocumentResult> SaveAsync(Stream source, string extension, long maxBytes, CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid().ToString("N") + extension;
        var path = Resolve(key); Directory.CreateDirectory(root);
        long total = 0;
        try {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920]; int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0) {
                total += read;
                if (total > maxBytes) throw new InvalidDataException("Document size exceeds limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        } catch { File.Delete(path); throw; }
        return new("Local", key, key, total);
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    { File.Delete(Resolve(key)); return Task.CompletedTask; }
}
