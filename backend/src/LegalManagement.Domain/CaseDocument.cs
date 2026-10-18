namespace LegalManagement.Domain;

public static class DocumentTypes
{
    public static readonly string[] All = ["PLEADING", "RESOLUTION", "EVIDENCE", "CONTRACT", "IDENTIFICATION", "CORRESPONDENCE", "OTHER"];
}
public class CaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string Extension { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string DocumentType { get; set; } = "OTHER";
    public string? Notes { get; set; }
    public string StorageProvider { get; set; } = "Local";
    public string StorageKey { get; set; } = "";
    public Guid UploadedByMembershipId { get; set; }
    public Membership UploadedByMembership { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByMembershipId { get; set; }
}
