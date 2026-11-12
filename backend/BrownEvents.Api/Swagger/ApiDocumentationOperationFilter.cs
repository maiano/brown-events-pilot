using System.Text.Json;
using BrownEvents.Api.Controllers;
using BrownEvents.Api.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BrownEvents.Api.Swagger;

/// <summary>Adds illustrative examples and schemas for anonymous response envelopes to OpenAPI only.</summary>
public class ApiDocumentationOperationFilter : IOperationFilter
{
    /// <summary>Documents existing operations without changing request handling or serialization.</summary>
    /// <param name="operation">The generated OpenAPI operation.</param>
    /// <param name="context">The action and schema-generation context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controller = context.MethodInfo.DeclaringType;
        if (controller != typeof(ConferencesController) &&
            controller != typeof(SessionsController) && controller != typeof(SpeakersController))
            return;

        foreach (var parameter in operation.Parameters.Where(p => p.Name == "id"))
            parameter.Example = new OpenApiInteger(1);

        var action = context.MethodInfo.Name;
        object? requestExample = null;
        object responseExample;

        if (controller == typeof(ConferencesController))
        {
            switch (action)
            {
                case nameof(ConferencesController.GetAll):
                    responseExample = new[] { ConferenceExample() };
                    break;
                case nameof(ConferencesController.GetById):
                    responseExample = ConferenceExample();
                    break;
                case nameof(ConferencesController.Create):
                case nameof(ConferencesController.Update):
                    requestExample = ConferenceFields();
                    responseExample = ConferenceExample();
                    break;
                case nameof(ConferencesController.GetSessions):
                    SetEnvelopeSchema(operation, context, isArray: true);
                    responseExample = new { data = new[] { SessionExample() } };
                    break;
                case nameof(ConferencesController.AddSession):
                    SetEnvelopeSchema(operation, context, isArray: false);
                    requestExample = SessionFields();
                    responseExample = new { data = SessionExample() };
                    break;
                case nameof(ConferencesController.GetRegistrations):
                    responseExample = new[] { RegistrationExample() };
                    break;
                case nameof(ConferencesController.Register):
                    requestExample = AttendeeFields();
                    responseExample = RegistrationExample();
                    break;
                default:
                    return;
            }
        }
        else if (controller == typeof(SessionsController))
        {
            if (action == nameof(SessionsController.Update))
                requestExample = SessionFields();
            responseExample = SessionExample();
        }
        else
        {
            if (action == nameof(SpeakersController.Create))
                requestExample = SpeakerFields();
            responseExample = action == nameof(SpeakersController.GetAll)
                ? new[] { SpeakerExample() }
                : SpeakerExample();
        }

        if (requestExample != null && operation.RequestBody != null)
        {
            foreach (var mediaType in operation.RequestBody.Content.Values)
                mediaType.Example = ToOpenApi(requestExample);
        }

        foreach (var mediaType in operation.Responses["200"].Content.Values)
        {
            mediaType.Examples["success"] = new OpenApiExample
            {
                Summary = "Illustrative success; IDs and navigation contents vary by database state",
                Value = ToOpenApi(responseExample)
            };
            if (controller == typeof(ConferencesController) && action == nameof(ConferencesController.GetSessions))
            {
                mediaType.Examples["empty"] = new OpenApiExample
                {
                    Summary = "No matching sessions, including an unmatched conference ID",
                    Value = ToOpenApi(new { data = Array.Empty<object>() })
                };
            }
        }
    }

    private static void SetEnvelopeSchema(OpenApiOperation operation, OperationFilterContext context, bool isArray)
    {
        var session = context.SchemaGenerator.GenerateSchema(typeof(Session), context.SchemaRepository);
        var schema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "data" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["data"] = isArray ? new OpenApiSchema { Type = "array", Items = session } : session
            }
        };
        operation.Responses["200"].Content["application/json"] = new OpenApiMediaType { Schema = schema };
    }

    private static IOpenApiAny ToOpenApi(object value) =>
        OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(value));

    private static Dictionary<string, object?> ConferenceFields() => new()
    {
        ["title"] = "BrownEvents Developer Conference",
        ["description"] = "Talks and workshops for developers.",
        ["location"] = "Providence",
        ["venue"] = "Main Hall",
        ["startDate"] = "2026-12-10T09:00:00Z",
        ["endDate"] = "2026-12-11T17:00:00Z",
        ["status"] = "UPCOMING",
        ["maxAttendees"] = 200,
        ["organizerName"] = "BrownEvents",
        ["website"] = "https://example.com/conference"
    };

    private static Dictionary<string, object?> SessionFields() => new()
    {
        ["title"] = "Practical API Design",
        ["description"] = "Designing predictable API contracts.",
        ["startTime"] = "2026-12-10T10:00:00Z",
        ["endTime"] = "2026-12-10T11:00:00Z",
        ["capacity"] = 50,
        ["speakerId"] = null,
        ["roomId"] = null
    };

    private static Dictionary<string, object?> AttendeeFields() => new()
    {
        ["firstName"] = "Alex",
        ["lastName"] = "Morgan",
        ["email"] = "alex.morgan@example.com"
    };

    private static Dictionary<string, object?> SpeakerFields() => new()
    {
        ["firstName"] = "Jamie",
        ["lastName"] = "Taylor",
        ["bio"] = "Software engineer and conference speaker.",
        ["email"] = "jamie.taylor@example.com"
    };

    private static Dictionary<string, object?> ConferenceExample() => new(ConferenceFields())
    {
        ["id"] = 1,
        ["sessions"] = Array.Empty<object>(),
        ["registrations"] = Array.Empty<object>()
    };

    private static Dictionary<string, object?> SessionExample()
    {
        var conference = ConferenceExample();
        // This session is already being serialized, so its back-reference is a cycle.
        conference["sessions"] = new object?[] { null };
        return new Dictionary<string, object?>(SessionFields())
        {
            ["id"] = 1,
            ["conferenceId"] = 1,
            ["conference"] = conference,
            ["speaker"] = null,
            ["room"] = null
        };
    }

    private static Dictionary<string, object?> SpeakerExample() => new(SpeakerFields())
    {
        ["id"] = 1,
        ["sessions"] = Array.Empty<object>()
    };

    private static object RegistrationExample()
    {
        var conference = ConferenceExample();
        conference["registrations"] = new object?[] { null };
        var attendee = new Dictionary<string, object?>(AttendeeFields())
        {
            ["id"] = 1,
            ["registrations"] = new object?[] { null }
        };
        return new
        {
            id = 1,
            registeredAt = "2026-11-12T12:00:00Z",
            status = "CONFIRMED",
            conferenceId = 1,
            attendeeId = 1,
            conference,
            attendee
        };
    }
}
