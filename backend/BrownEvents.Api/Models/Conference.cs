namespace BrownEvents.Api.Models;

/// <summary>A conference and its related sessions and registrations.</summary>
public class Conference
{
    /// <summary>The integer conference identifier. Omit on creation to use database-generated identity.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable title; defaults to an empty string.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>The description, or null.</summary>
    public string? Description { get; set; }
    /// <summary>The location, or null.</summary>
    public string? Location { get; set; }
    /// <summary>The venue, or null.</summary>
    public string? Venue { get; set; }
    /// <summary>The non-nullable conference start date and time.</summary>
    public DateTime StartDate { get; set; }
    /// <summary>The non-nullable conference end date and time.</summary>
    public DateTime EndDate { get; set; }
    /// <summary>The non-nullable status string; defaults to UPCOMING. Conference updates uppercase this value; no enum is enforced.</summary>
    public string Status { get; set; } = "UPCOMING";
    /// <summary>The attendee limit, or null. Registration does not enforce this limit.</summary>
    public int? MaxAttendees { get; set; }
    /// <summary>The organizer name, or null.</summary>
    public string? OrganizerName { get; set; }
    /// <summary>The website string, or null; no URL validation is applied.</summary>
    public string? Website { get; set; }

    /// <summary>The non-nullable session collection, initialized empty. Lazy loading may populate it; cyclic references can serialize as null.</summary>
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    /// <summary>The non-nullable registration collection, initialized empty. Lazy loading may populate it; cyclic references can serialize as null.</summary>
    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
