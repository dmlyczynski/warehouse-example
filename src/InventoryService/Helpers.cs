using System.Reflection;
using OpenTelemetry.Resources;

namespace InventoryService;

public static class Helpers
{
    public static void ConfigureResource(ResourceBuilder resource)
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();
        var assemblyVersion = assembly.Version?.ToString() ?? "1.0";
        resource.AddService(Instrumentor.ServiceName, serviceVersion: assemblyVersion)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["app.group"] = "main"
            });
    }
}