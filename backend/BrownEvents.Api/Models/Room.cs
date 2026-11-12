namespace BrownEvents.Api.Models;

/// <summary>A room and its related sessions; there is no room controller.</summary>
public class Room
{
    /// <summary>The integer room identifier.</summary>
    public int Id { get; set; }
    /// <summary>The non-nullable room name; defaults to an empty string.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The non-nullable integer room capacity.</summary>
    public int Capacity { get; set; }
    /// <summary>The location, or null.</summary>
    public string? Location { get; set; }

    /// <summary>The non-nullable session collection, initialized empty. Lazy loading may populate it; cyclic references can serialize as null.</summary>
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
