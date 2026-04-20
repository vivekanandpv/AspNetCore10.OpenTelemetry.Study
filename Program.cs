using AspNetCore10.OpenTelemetry.Study;
using AspNetCore10.OpenTelemetry.Study.Config;
using AspNetCore10.OpenTelemetry.Study.DAL;
using AspNetCore10.OpenTelemetry.Study.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AspNetCore10.OpenTelemetry.Study;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // Configure EF Core with SQLite
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Register Customer Service
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(
                ResourceBuilder.CreateDefault().AddService("AspNetCore10.OpenTelemetry.Study"));
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();      // same endpoint picked up from OTEL_EXPORTER_OTLP_ENDPOINT
        });

        // Configure OpenTelemetry
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("AspNetCore10.OpenTelemetry.Study")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AspNetCore10.OpenTelemetry.Study"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(Telemetry.ServiceName)
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AspNetCore10.OpenTelemetry.Study"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}