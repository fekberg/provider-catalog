using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ProviderCatalog.Hosting;

/// <summary>
/// Gives the catalog a public HTTPS address with a dev tunnel, so the Copilot cloud agent can reach it.
/// Runs when Catalog:Tunnel is true (the "tunnel" launch profile). The first time, it creates a tunnel
/// that allows anonymous access; the catalog's access tokens still protect every request.
/// </summary>
public sealed partial class DevTunnel(
    IServer server,
    StartupMessage startupMessage,
    IWebHostEnvironment environment,
    ILogger<DevTunnel> logger) : BackgroundService
{
    private const string Cli = "devtunnel";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await WaitForStartAsync(stoppingToken))
        {
            return;
        }

        var port = GetPort();

        if (!await IsInstalledAsync(stoppingToken))
        {
            Console.WriteLine();
            Console.WriteLine("The dev tunnel CLI isn't installed, so the catalog only runs on localhost.");
            Console.WriteLine("Install it, open a new terminal, and start the catalog again:");
            Console.WriteLine("  Windows:  winget install Microsoft.devtunnel");
            Console.WriteLine("  macOS:    brew install --cask devtunnel");
            Console.WriteLine("  Linux:    curl -sL https://aka.ms/DevTunnelCliInstall | bash");
            return;
        }

        if (!await EnsureSignedInAsync(stoppingToken))
        {
            return;
        }

        // A tunnel can expire or be deleted, so if hosting the saved one fails, create a new one once.
        for (var attempt = 0; attempt < 2 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            var tunnelId = LocalSettings.GetTunnelId(environment.ContentRootPath)
                ?? await CreateTunnelAsync(port, stoppingToken);

            if (tunnelId is null)
            {
                return;
            }

            if (await HostAsync(tunnelId, port, stoppingToken))
            {
                return;
            }

            logger.LogWarning("Couldn't host dev tunnel {TunnelId}. Creating a new one.", tunnelId);
            LocalSettings.SetTunnelId(environment.ContentRootPath, null);
        }
    }

    private async Task<bool> WaitForStartAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAny(startupMessage.Written, Task.Delay(Timeout.Infinite, stoppingToken));
        return startupMessage.Written.IsCompleted;
    }

    private int GetPort()
    {
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault(a => a.StartsWith("http://"))
            ?? "http://localhost:5180";
        return new Uri(address.Replace("*", "localhost").Replace("+", "localhost")).Port;
    }

    private static async Task<bool> IsInstalledAsync(CancellationToken stoppingToken)
    {
        try
        {
            return (await RunAsync(stoppingToken, "--version")).ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static async Task<bool> EnsureSignedInAsync(CancellationToken stoppingToken)
    {
        var status = await RunAsync(stoppingToken, "user", "show");

        if (status.ExitCode == 0 && !status.Output.Contains("not logged in", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Console.WriteLine();
        Console.WriteLine("Sign in to dev tunnels with your GitHub account. A browser window opens for you.");

        // Not redirected, so the sign-in prompt shows up in this terminal.
        using var login = Process.Start(new ProcessStartInfo(Cli, ["user", "login", "-g"]) { UseShellExecute = false })!;
        await login.WaitForExitAsync(stoppingToken);

        if (login.ExitCode != 0)
        {
            Console.WriteLine("Signing in didn't work. Run 'devtunnel user login -g' yourself, then start the catalog again.");
            return false;
        }

        return true;
    }

    private async Task<string?> CreateTunnelAsync(int port, CancellationToken stoppingToken)
    {
        var tunnelId = $"provider-catalog-{Convert.ToHexStringLower(Guid.NewGuid().ToByteArray())[..8]}";

        var created = await RunAsync(stoppingToken, "create", tunnelId, "--allow-anonymous", "--expiration", "30d");
        var portAdded = created.ExitCode == 0
            ? await RunAsync(stoppingToken, "port", "create", tunnelId, "-p", port.ToString())
            : created;

        if (portAdded.ExitCode != 0)
        {
            Console.WriteLine();
            Console.WriteLine("Couldn't create a dev tunnel:");
            Console.WriteLine(portAdded.Output.Trim());
            return null;
        }

        LocalSettings.SetTunnelId(environment.ContentRootPath, tunnelId);
        return tunnelId;
    }

    /// <summary>Hosts the tunnel until the catalog stops. Returns false if the tunnel couldn't be hosted.</summary>
    private static async Task<bool> HostAsync(string tunnelId, int port, CancellationToken stoppingToken)
    {
        using var host = Process.Start(new ProcessStartInfo(Cli, ["host", tunnelId])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var announced = false;
        host.ErrorDataReceived += (_, _) => { };
        host.BeginErrorReadLine();

        try
        {
            while (await host.StandardOutput.ReadLineAsync(stoppingToken) is { } line)
            {
                var match = TunnelUrl().Match(line);

                if (!announced && match.Success && match.Value.Contains($"-{port}."))
                {
                    announced = true;
                    var mcpAddress = match.Value.TrimEnd('/') + "/mcp";
                    Console.WriteLine();
                    Console.WriteLine($"  Public MCP address  {mcpAddress}");
                    Console.WriteLine();
                    Console.WriteLine("  Use it in the GitHub Copilot app and in the repository's MCP configuration.");
                    Console.WriteLine("  It stays the same every time you start the catalog this way, for 30 days.");
                    Console.WriteLine();
                }
            }

            await host.WaitForExitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // The catalog is shutting down.
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
            }
        }

        return announced;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(CancellationToken stoppingToken, params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(Cli, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var output = process.StandardOutput.ReadToEndAsync(stoppingToken);
        var error = process.StandardError.ReadToEndAsync(stoppingToken);
        await process.WaitForExitAsync(stoppingToken);

        return (process.ExitCode, await output + await error);
    }

    [GeneratedRegex(@"https://[A-Za-z0-9.-]+\.devtunnels\.ms/?")]
    private static partial Regex TunnelUrl();
}
