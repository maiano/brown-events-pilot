using BrownEvents.Api.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BrownEvents.Api.Swagger;

/// <summary>Documents null navigation collection entries emitted by IgnoreCycles.</summary>
public class NullableNavigationItemsSchemaFilter : ISchemaFilter
{
    /// <summary>Preserves entity item constraints while allowing a cycle-related null.</summary>
    /// <param name="schema">The generated entity schema.</param>
    /// <param name="context">The schema-generation context.</param>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(Conference) && context.Type != typeof(Attendee) &&
            context.Type != typeof(Speaker) && context.Type != typeof(Room))
            return;

        foreach (var property in schema.Properties.Values)
        {
            if (property.Type != "array" || property.Items?.Reference == null)
                continue;

            property.Items = new OpenApiSchema
            {
                OneOf = new List<OpenApiSchema>
                {
                    property.Items,
                    // The enum makes this branch null-only, avoiding oneOf overlap with entities.
                    new() { Type = "object", Nullable = true, Enum = new List<IOpenApiAny> { new OpenApiNull() } }
                }
            };
        }
    }
}
