namespace BrownEvents.Api.Models;

/// <summary>A conference-level registration connecting a conference and an attendee; it has no session field.</summary>
public class Registration
{
    /// <summary>The integer registration identifier.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable registration date and time; the registration service assigns the current UTC time.</summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    /// <summary>The non-nullable status string; defaults to CONFIRMED and is set to CONFIRMED by the registration service. No enum is enforced.</summary>
    public string Status { get; set; } = "CONFIRMED";

    /// <summary>The non-nullable conference identifier.</summary>
    public int ConferenceId { get; set; }
    /// <summary>The non-nullable attendee identifier.</summary>
    public int AttendeeId { get; set; }

    /// <summary>The required conference relationship. Initialized null until populated; it can serialize as null when absent or cyclic.</summary>
    public virtual Conference Conference { get; set; } = null!;
    /// <summary>The required attendee relationship. Initialized null until populated; it can serialize as null when absent or cyclic.</summary>
    public virtual Attendee Attendee { get; set; } = null!;
}
