namespace BrownEvents.Api.Models;

/// <summary>An attendee supplied when registering for a conference.</summary>
public class Attendee
{
    /// <summary>The integer attendee identifier. Omit when registering a new attendee to use database-generated identity.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable first name; defaults to an empty string. Existing attendees reused by email keep their stored name.</summary>
    public string FirstName { get; set; } = string.Empty;
    /// <summary>The non-nullable last name; defaults to an empty string. Existing attendees reused by email keep their stored name.</summary>
    public string LastName { get; set; } = string.Empty;
    /// <summary>The non-nullable email string; defaults to an empty string. Used to find an existing attendee without explicit normalization or format validation.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The non-nullable registration collection, initialized empty. Lazy loading may populate it; cyclic references can serialize as null. Omit from registration request examples.</summary>
    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
