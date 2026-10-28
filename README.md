# Provider catalog (sample)

The sample provider catalog for the course **Advanced GitHub Copilot**. In the course scenario, it's an internal service run by the partnerships team. It keeps every delivery provider's integration spec, version by version, and makes the specs available to GitHub Copilot through an MCP server.

You only need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The project has no other dependencies.

## Run it

```
dotnet run
```

The first time, the catalog creates two access tokens: one for you, and one for your repository. It saves them in `catalog.local.json` and prints them together with its addresses:

```
Provider catalog is running.

  Web pages           http://localhost:5180
  MCP address         http://localhost:5180/mcp

  Your token          3f9c...
  Repository token    a71e...
```

Open http://localhost:5180 and sign in with your token to read the specs. In the GitHub Copilot app, add the MCP address as an HTTP server, with an `Authorization` header set to `Bearer <your token>`.

## Run it with a public address

The Copilot cloud agent runs on GitHub, so it can't reach `localhost`. To give the catalog a public HTTPS address, start it like this instead:

```
dotnet run --launch-profile tunnel
```

This uses a dev tunnel. The first time, the catalog walks you through it:

1. If the dev tunnel CLI isn't installed, it prints the install command (on Windows, `winget install Microsoft.devtunnel`). Install it, open a new terminal and run the command again.
2. It asks you to sign in to dev tunnels with your GitHub account in the browser.
3. It creates a tunnel and prints the public address:

   ```
   Public MCP address  https://provider-catalog-1a2b3c4d-5180.euw.devtunnels.ms/mcp
   ```

After that, the same command gives you the same address every time, for 30 days. If the tunnel expires, the catalog creates a new one and prints the new address. Use the public address both in the GitHub Copilot app and in your repository's MCP configuration, with the repository token in the repository's secret.

The tunnel allows anonymous access so the cloud agent can reach it, but the catalog still rejects every request without a valid token.

## What's inside

- **An MCP endpoint** at `/mcp`, using the Streamable HTTP transport. Every request needs an `Authorization: Bearer <token>` header. It offers four tools:

  | Tool | What it does |
  | --- | --- |
  | `find_provider` | Finds a provider by name and returns its id and versions |
  | `get_spec` | Returns one version of a provider's spec |
  | `list_providers` | Lists every provider |
  | `submit_spec_feedback` | Sends feedback to the partnerships team (the only tool that changes anything) |

- **Web pages** at `/` for reading the specs.
- **The specs** in `data/<provider id>/<version>.json`. To add a provider or a version, add a file and restart.

The terminal logs every tool call with the name of the token that made it, for example `repository called get_spec`.

## Notes

- Keep `catalog.local.json` to yourself. It holds your tokens and is ignored by Git. Delete it to get new tokens.
- Feedback is kept in memory, so restarting the catalog clears it.

## Disclaimer

This demo uses fictional names and people. No affiliation with or endorsement by any real company.
