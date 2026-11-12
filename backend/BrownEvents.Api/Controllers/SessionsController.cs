using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrownEvents.Api.Controllers;

[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _service;

    public SessionsController(ISessionService service)
    {
        _service = service;
    }

    /// <summary>Get a session by ID.</summary>
    /// <remarks>Returns the session entity, including conference, speaker and room navigation properties when populated. Optional relationships and cyclic references can serialize as null.</remarks>
    /// <param name="id">The integer session ID in the path.</param>
    /// <response code="200">The matching session.</response>
    /// <response code="404">No session has this ID. The response body is empty.</response>
    [ProducesResponseType(typeof(Session), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var session = await _service.GetByIdAsync(id);
        if (session is null) return NotFound();
        return Ok(session);
    }

    /// <summary>Update a session.</summary>
    /// <remarks>The path ID selects the record. Updates title, description, startTime, endTime, capacity, speakerId and roomId. Body ID, conferenceId and navigation objects are not copied. Omitted editable fields use model defaults; omitted speakerId and roomId become null. Non-null relationship IDs must refer to existing records.</remarks>
    /// <param name="id">The integer session ID to update.</param>
    /// <param name="session">The replacement editable session values; see the JSON example.</param>
    /// <response code="200">The updated session.</response>
    /// <response code="404">No session has this ID. The response body is empty.</response>
    [ProducesResponseType(typeof(Session), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Session session)
    {
        var updated = await _service.UpdateAsync(id, session);
        if (updated is null) return NotFound();
        return Ok(updated);
    }
}
