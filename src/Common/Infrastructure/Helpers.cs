using System.Reflection;
using OpenTelemetry.Resources;

namespace Common.Infrastructure;

public static class Helpers
{
    public static void ConfigureResource(ResourceBuilder resource, string serviceName)
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();
        var assemblyVersion = assembly.Version?.ToString() ?? "1.0";
        resource.AddService(serviceName, serviceVersion: assemblyVersion)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["app.group"] = "main"
            });
    }
}