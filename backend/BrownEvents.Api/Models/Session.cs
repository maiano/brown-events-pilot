namespace BrownEvents.Api.Models;

/// <summary>A session belonging to a conference, with optional speaker and room relationships.</summary>
public class Session
{
    /// <summary>The integer session identifier. Omit on creation to use database-generated identity.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable title; defaults to an empty string.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>The description, or null.</summary>
    public string? Description { get; set; }
    /// <summary>The non-nullable session start date and time.</summary>
    public DateTime StartTime { get; set; }
    /// <summary>The non-nullable session end date and time.</summary>
    public DateTime EndTime { get; set; }
    /// <summary>The integer capacity; no range or registration-capacity validation is implemented.</summary>
    public int Capacity { get; set; }

    /// <summary>The non-nullable conference identifier. Creation uses the path conference ID; session updates ignore this body field.</summary>
    public int ConferenceId { get; set; }
    /// <summary>The related speaker identifier, or null.</summary>
    public int? SpeakerId { get; set; }
    /// <summary>The related room identifier, or null.</summary>
    public int? RoomId { get; set; }

    /// <summary>The required conference relationship. Initialized null until populated; it can serialize as null when absent or cyclic.</summary>
    public virtual Conference Conference { get; set; } = null!;
    /// <summary>The optional speaker relationship, or null. Cyclic references also serialize as null.</summary>
    public virtual Speaker? Speaker { get; set; }
    /// <summary>The optional room relationship, or null. Cyclic references also serialize as null.</summary>
    public virtual Room? Room { get; set; }
}
