using BrownEvents.Api.Data;
using BrownEvents.Api.Services;
using BrownEvents.Api.Swagger;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton<AppDbContext>(sp =>
{
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString)
        .UseLazyLoadingProxies()
        .EnableSensitiveDataLogging();
    return new AppDbContext(optionsBuilder.Options);
});

builder.Services.AddScoped<IConferenceService, ConferenceService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ISpeakerService, SpeakerService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BrownEvents API",
        Version = "v1",
        Description = "Conferences, sessions, speakers and conference-level registrations. " +
            "Responses include entity navigation properties; cyclic references serialize as null. " +
            "Lazy-loading proxies can expose additional properties such as lazyLoader; " +
            "the schemas describe entity fields, and navigation graphs vary with database and tracking state. " +
            "Examples are illustrative; use IDs returned by this instance when trying requests."
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "BrownEvents.Api.xml"));
    options.UseAllOfToExtendReferenceSchemas();
    options.SchemaFilter<NullableNavigationItemsSchemaFilter>();
    options.SchemaFilter<NullableNavigationSchemaFilter>();
    options.OperationFilter<ApiDocumentationOperationFilter>();
});

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.MapControllers();

// Seed demo data on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    await DataSeeder.SeedAsync(context);
}

app.Run();
