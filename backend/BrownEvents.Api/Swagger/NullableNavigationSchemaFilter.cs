using BrownEvents.Api.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BrownEvents.Api.Swagger;

/// <summary>Documents nullable singular entity relationships without weakening entity schemas.</summary>
public class NullableNavigationSchemaFilter : ISchemaFilter
{
    /// <summary>Replaces nullable reference wrappers with disjoint entity and null alternatives.</summary>
    /// <param name="schema">The generated entity schema.</param>
    /// <param name="context">The schema-generation context.</param>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(Session) && context.Type != typeof(Registration))
            return;

        foreach (var property in schema.Properties.Values)
        {
            if (!property.Nullable || property.AllOf.Count != 1 || property.AllOf[0].Reference == null)
                continue;

            property.OneOf = new List<OpenApiSchema>
            {
                property.AllOf[0],
                // Only null matches this branch; entities retain all referenced constraints.
                new() { Type = "object", Nullable = true, Enum = new List<IOpenApiAny> { new OpenApiNull() } }
            };
            property.AllOf = new List<OpenApiSchema>();
            property.Nullable = false;
        }
    }
}
