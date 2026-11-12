using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrownEvents.Api.Controllers;

[Route("api/conferences")]
public class ConferencesController : ControllerBase
{
    private readonly IConferenceService _service;
    private readonly IRegistrationService _registrationService;

    public ConferencesController(IConferenceService service, IRegistrationService registrationService)
    {
        _service = service;
        _registrationService = registrationService;
    }

    /// <summary>List conferences.</summary>
    /// <remarks>Returns an unpaged entity array, or an empty array. Navigation properties may be populated by lazy loading; cyclic references serialize as null.</remarks>
    /// <response code="200">The conference array.</response>
    [ProducesResponseType(typeof(List<Conference>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var conferences = await _service.GetAllAsync();
        return Ok(conferences);
    }

    /// <summary>Get a conference by ID.</summary>
    /// <remarks>Returns the conference entity with navigation properties. Cyclic references serialize as null.</remarks>
    /// <param name="id">The integer conference ID in the path.</param>
    /// <response code="200">The matching conference.</response>
    /// <response code="404">No conference has this ID. The response body is empty.</response>
    [ProducesResponseType(typeof(Conference), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var conference = await _service.GetByIdAsync(id);
        if (conference is null) return NotFound();
        return Ok(conference);
    }

    /// <summary>Create a conference.</summary>
    /// <remarks>Returns 200 with the created entity. Omit the ID and navigation collections for a simple create. Status defaults to UPCOMING and is not normalized on creation. No enum or date-range validation is implemented.</remarks>
    /// <param name="conference">The conference entity fields to create; see the JSON example.</param>
    /// <response code="200">The created conference.</response>
    [ProducesResponseType(typeof(Conference), StatusCodes.Status200OK)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Conference conference)
    {
        var created = await _service.CreateAsync(conference);
        return Ok(created);
    }

    /// <summary>Update a conference.</summary>
    /// <remarks>The path ID selects the record; body ID and navigation collections are not copied. All scalar editable fields are assigned, so omitted fields use model defaults and can overwrite stored values. Status is uppercased.</remarks>
    /// <param name="id">The integer conference ID to update.</param>
    /// <param name="conference">The replacement scalar conference values; see the JSON example.</param>
    /// <response code="200">The updated conference.</response>
    /// <response code="404">No conference has this ID. The response body is empty.</response>
    [ProducesResponseType(typeof(Conference), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Conference conference)
    {
        var updated = await _service.UpdateAsync(id, conference);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    /// <summary>List sessions for a conference.</summary>
    /// <remarks>Returns an object with a data array. No parent-existence check is performed: an unmatched conference ID returns 200 with an empty data array.</remarks>
    /// <param name="id">The integer conference ID used to filter sessions.</param>
    /// <response code="200">An object whose data property contains the session array, possibly empty.</response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("{id}/sessions")]
    public async Task<IActionResult> GetSessions(int id)
    {
        var sessions = await _service.GetSessionsAsync(id);
        return Ok(new { data = sessions });
    }

    /// <summary>Add a session to a conference.</summary>
    /// <remarks>Returns 200 with an object whose data property is the created session. The path ID overwrites body conferenceId. Omit the ID and navigation objects for a simple create. Non-null speakerId and roomId must refer to existing records. There is no explicit parent-existence check or controlled parent-not-found response.</remarks>
    /// <param name="id">The integer conference ID assigned to the new session.</param>
    /// <param name="session">The session entity fields to create; the path supplies conferenceId.</param>
    /// <response code="200">An object whose data property contains the created session.</response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpPost("{id}/sessions")]
    public async Task<IActionResult> AddSession(int id, [FromBody] Session session)
    {
        var created = await _service.AddSessionAsync(id, session);
        return Ok(new { data = created });
    }

    /// <summary>List registrations for a conference.</summary>
    /// <remarks>Returns an unpaged registration entity array. No parent-existence check is performed: an unmatched conference ID returns 200 with an empty array. Registrations are conference-level.</remarks>
    /// <param name="id">The integer conference ID used to filter registrations.</param>
    /// <response code="200">The registration array, possibly empty.</response>
    [ProducesResponseType(typeof(List<Registration>), StatusCodes.Status200OK)]
    [HttpGet("{id}/registrations")]
    public async Task<IActionResult> GetRegistrations(int id)
    {
        var registrations = await _service.GetRegistrationsAsync(id);
        return Ok(registrations);
    }

    /// <summary>Register an attendee for a conference.</summary>
    /// <remarks>Accepts an Attendee entity, not a Registration. An existing attendee is reused by email without updating their name; otherwise a new attendee is saved. A new registration is always created with current UTC time and CONFIRMED status. No duplicate, capacity or explicit conference-existence check is performed. There is no sessionId field or session-level registration. Omit ID and navigation collections for a simple request.</remarks>
    /// <param name="id">The integer conference ID assigned to the registration.</param>
    /// <param name="attendee">The attendee details; firstName, lastName and email form the example body.</param>
    /// <response code="200">The created conference-level registration entity.</response>
    [ProducesResponseType(typeof(Registration), StatusCodes.Status200OK)]
    [HttpPost("{id}/register")]
    public async Task<IActionResult> Register(int id, [FromBody] Attendee attendee)
    {
        var registration = await _registrationService.RegisterAttendeeAsync(id, attendee);
        return Ok(registration);
    }
}
