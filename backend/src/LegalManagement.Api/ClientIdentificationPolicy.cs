using LegalManagement.Domain;

namespace LegalManagement.Api;

public static class ClientIdentificationPolicy
{
    public static (string Type, string Number) NormalizeAndValidate(string clientType, string? identificationType, string? number)
    {
        var type = (identificationType ?? "").Trim().ToUpperInvariant();
        var normalizedNumber = (number ?? "").Trim().ToUpperInvariant();
        if (type.Length == 0 || normalizedNumber.Length == 0) throw new ApiException(400, "La identificación es obligatoria.");
        var allowed = clientType == ClientTypes.Person ? IdentificationTypes.Person : IdentificationTypes.Company;
        if (!allowed.Contains(type)) throw new ApiException(400, clientType == ClientTypes.Person
            ? "Una persona natural solo admite CEDULA o PASSPORT." : "Una persona jurídica solo admite RUC.");
        // Las reglas formales panameñas siguen pendientes de una especificación aprobada.
        return (type, normalizedNumber);
    }
}
