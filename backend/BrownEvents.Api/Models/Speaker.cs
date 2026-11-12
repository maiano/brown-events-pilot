namespace BrownEvents.Api.Models;

/// <summary>A speaker and their related sessions.</summary>
public class Speaker
{
    /// <summary>The integer speaker identifier. Omit on creation to use database-generated identity.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable first name; defaults to an empty string.</summary>
    public string FirstName { get; set; } = string.Empty;
    /// <summary>The non-nullable last name; defaults to an empty string.</summary>
    public string LastName { get; set; } = string.Empty;
    /// <summary>The biography, or null.</summary>
    public string? Bio { get; set; }
    /// <summary>The email string, or null; no email-format validation is applied.</summary>
    public string? Email { get; set; }

    /// <summary>The non-nullable session collection, initialized empty. Lazy loading may populate it; cyclic references can serialize as null.</summary>
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
