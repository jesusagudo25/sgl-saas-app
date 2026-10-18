using System.ComponentModel.DataAnnotations;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LegalManagement.Api;

public class UploadDocumentRequest
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required] public string DocumentType { get; set; } = "";
    [StringLength(4000)] public string? Notes { get; set; }
}

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/cases/{caseId:guid}/documents")]
public class CaseDocumentsController(AppDbContext db, ITenantContext tenant, IDocumentStorage storage, IOptions<DocumentOptions> options) : ControllerBase
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase) {
        [".pdf"] = "application/pdf", [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel", [".txt"] = "text/plain", [".csv"] = "text/csv",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png" };
    private IQueryable<CaseDocument> Own(Guid caseId) => db.CaseDocuments.Where(x => x.OrganizationId == tenant.OrganizationId && x.CaseId == caseId && !x.IsDeleted);
    private async Task OwnCase(Guid caseId)
    { if (!await db.Cases.AnyAsync(x => x.Id == caseId && x.OrganizationId == tenant.OrganizationId)) throw new ApiException(404, "Caso no encontrado."); }
    private Task<Guid> Member() => db.Memberships.Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.Status == "ACTIVE").Select(x => x.Id).SingleAsync();
    private IQueryable<CaseDocumentDto> Project(IQueryable<CaseDocument> query) => query.Select(x => new CaseDocumentDto(x.Id, x.OriginalFileName,
        x.Extension, x.ContentType, x.SizeBytes, x.DocumentType, x.Notes,
        db.Users.Where(u => u.Id == x.UploadedByMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault() ?? "", new DateTimeOffset(DateTime.SpecifyKind(x.UploadedAt, DateTimeKind.Utc))));

    [HttpGet]
    public async Task<object> List(Guid caseId)
    { await OwnCase(caseId); return await Project(Own(caseId).AsNoTracking().OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id)).ToListAsync(); }

    [HttpPost, Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid caseId, [FromForm] UploadDocumentRequest request, CancellationToken ct)
    {
        await OwnCase(caseId);
        var file = request.File;
        if (file.Length == 0) throw new ApiException(400, "El archivo está vacío.");
        if (file.Length > options.Value.MaxBytes) throw new ApiException(413, "El archivo supera el tamaño máximo permitido.");
        var name = file.FileName.Replace('\\', '/').Split('/').Last();
        name = new string(name.Where(c => !char.IsControl(c) && !"<>:\"|?*".Contains(c)).ToArray()).Trim().TrimEnd('.');
        if (name.Length == 0 || name.Length > 240) throw new ApiException(400, "Nombre de archivo inválido (máximo 240 caracteres).");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!MimeTypes.TryGetValue(extension, out var mime) || !string.Equals(file.ContentType.Split(';')[0].Trim(), mime, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(400, "Extensión o tipo MIME no permitido.");
        var type = request.DocumentType.Trim().ToUpperInvariant();
        if (!DocumentTypes.All.Contains(type)) throw new ApiException(400, "Tipo de documento inválido.");
        var member = await Member();
        StoredDocumentResult saved;
        try { await using var input = file.OpenReadStream(); saved = await storage.SaveAsync(input, extension, options.Value.MaxBytes, ct); }
        catch (InvalidDataException) { throw new ApiException(413, "El archivo supera el tamaño máximo permitido."); }
        var entity = new CaseDocument { OrganizationId = tenant.OrganizationId, CaseId = caseId, OriginalFileName = name,
            StoredFileName = saved.FileName, Extension = extension, ContentType = mime, SizeBytes = saved.SizeBytes, DocumentType = type,
            Notes = request.Notes?.Trim(), StorageProvider = saved.Provider, StorageKey = saved.Key, UploadedByMembershipId = member };
        try { db.CaseDocuments.Add(entity); await db.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(saved.Key, CancellationToken.None); throw; }
        return StatusCode(201, await Project(Own(caseId).AsNoTracking().Where(x => x.Id == entity.Id)).SingleAsync(ct));
    }
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid caseId, Guid id, CancellationToken ct)
    {
        await OwnCase(caseId);
        var entity = await Own(caseId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiException(404, "Documento no encontrado.");
        try { return File(await storage.OpenReadAsync(entity.StorageKey, ct), entity.ContentType, entity.OriginalFileName); }
        catch (FileNotFoundException) { throw new ApiException(404, "Archivo no disponible."); }
        catch (DirectoryNotFoundException) { throw new ApiException(404, "Archivo no disponible."); }
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid caseId, Guid id)
    {
        await OwnCase(caseId);
        var entity = await Own(caseId).SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Documento no encontrado.");
        entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow; entity.DeletedByMembershipId = await Member();
        await db.SaveChangesAsync(); return NoContent();
    }
}
