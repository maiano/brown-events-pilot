using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrownEvents.Api.Controllers;

[Route("api/speakers")]
public class SpeakersController : ControllerBase
{
    private readonly ISpeakerService _service;

    public SpeakersController(ISpeakerService service)
    {
        _service = service;
    }

    /// <summary>List speakers.</summary>
    /// <remarks>Returns an unpaged entity array, or an empty array. Related sessions may be populated by lazy loading; cyclic references serialize as null.</remarks>
    /// <response code="200">The speaker array.</response>
    [ProducesResponseType(typeof(List<Speaker>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var speakers = await _service.GetAllAsync();
        return Ok(speakers);
    }

    /// <summary>Create a speaker.</summary>
    /// <remarks>Returns 200 with the created entity. Omit ID and the sessions collection for a simple create. Biography and email may be null; no email-format validation is implemented.</remarks>
    /// <param name="speaker">The speaker entity fields to create; see the JSON example.</param>
    /// <response code="200">The created speaker.</response>
    [ProducesResponseType(typeof(Speaker), StatusCodes.Status200OK)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Speaker speaker)
    {
        var created = await _service.CreateAsync(speaker);
        return Ok(created);
    }
}
