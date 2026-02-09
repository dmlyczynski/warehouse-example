using Microsoft.AspNetCore.Mvc;
using TestApp;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

builder.Configuration.Bind(configuration);

builder.Services.AddSingleton<JwtTokenService>();

// builder.WebHost.ConfigureKestrel(serverOptions =>
// {
//     serverOptions.ListenAnyIP(1234);
// });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/auth/token",
        ([FromQuery] string role, JwtTokenService jwt) =>
        {
            role = string.IsNullOrWhiteSpace(role) ? "write" : role;

            var token = jwt.GenerateToken("TestUser", role);

            return Results.Ok(new
            {
                access_token = token
            });
        })
    .AllowAnonymous()
    .WithName("GetToken")
    .WithTags("Auth");

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Open API V");
});

app.Run();
