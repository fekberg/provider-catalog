using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using ProviderCatalog.Auth;

namespace ProviderCatalog.Hosting;

/// <summary>Prints where the catalog is running and which token to use, once it has started.</summary>
public sealed class StartupMessage
{
    private readonly TaskCompletionSource _written = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the message is printed, so anything printed later shows up below it.</summary>
    public Task Written => _written.Task;

    public void Write(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?
            .Addresses.FirstOrDefault()?.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost")
            ?? "http://localhost:5180";

        var owners = app.Services.GetRequiredService<AccessTokens>().Owners;

        Console.WriteLine();
        Console.WriteLine("Provider catalog is running.");
        Console.WriteLine();
        Console.WriteLine($"  Web pages           {address}");
        Console.WriteLine($"  MCP address         {address.TrimEnd('/')}/mcp");
        Console.WriteLine();

        // Show the tokens the catalog generated. Tokens you set yourself are only listed by name.
        if (app.Configuration["Catalog:Tokens:me"] is { } mine && app.Configuration["Catalog:Tokens:repository"] is { } repository)
        {
            Console.WriteLine($"  Your token          {mine}");
            Console.WriteLine($"  Repository token    {repository}");
            Console.WriteLine($"  (both saved in {LocalSettings.FileName})");
        }
        else
        {
            Console.WriteLine($"  Tokens for          {string.Join(", ", owners)}");
        }

        Console.WriteLine();
        _written.TrySetResult();
    }
}
