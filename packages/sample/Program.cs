using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsFrontendAPI;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Identity.Client;

// --use-msal -> MSAL flow; otherwise DefaultAzureCredential.
bool useMsal = args.Any(a => string.Equals(a, "--use-msal", StringComparison.OrdinalIgnoreCase));
// --insecure -> Skip TLS certificate validation (dev/test only).
bool insecureTls = args.Any(a => string.Equals(a, "--insecure", StringComparison.OrdinalIgnoreCase));

// Frontend base URL (equivalent to $frontend in Invoke-Frontend).
var endpoint = new Uri(
    Environment.GetEnvironmentVariable("ANALYTICS_FRONTEND_ENDPOINT")
        ?? "http://localhost:8080");

// AAD scope requested for the access token.
var scope = Environment.GetEnvironmentVariable("ANALYTICS_FRONTEND_SCOPE")
    ?? "https://management.azure.com/.default";

// Pick the token source based on the CLI flag.
TokenCredential credential = useMsal
    ? new MsalTokenCredential(
        clientId: Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
            ?? throw new InvalidOperationException("AZURE_CLIENT_ID must be set when using --use-msal."),
        tenantId: Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "organizations")
    : new DefaultAzureCredential();

// Stamps "Authorization: Bearer <token>" on every outgoing request (handles caching + refresh).
var options = new CollaborationClientOptions();

if (insecureTls)
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    options.Transport = new HttpClientTransport(new HttpClient(handler));
}

options.AddPolicy(
    new BearerTokenAuthenticationPolicy(credential, scope),
    HttpPipelinePosition.PerCall);

// SDK client handles URL, api-version, JSON serialization, and HTTP method dispatch.
var client = new CollaborationClient(endpoint, options);

Console.WriteLine(
    $"AnalyticsFrontend CollaborationClient initialized for {endpoint} " +
    $"(auth: {(useMsal ? "MSAL" : "DefaultAzureCredential")}, insecureTls: {insecureTls}).");

try
{
    // GET /collaborations?api-version=... with the bearer header attached by the policy.
    var response = await client.GetGetsAsync(null, null);
    Console.WriteLine($"GET / -> HTTP {response.Status}");
    Console.WriteLine(response.Content);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
}

// Adapter exposing MSAL as an Azure.Core TokenCredential.
internal sealed class MsalTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _app;

    public MsalTokenCredential(string clientId, string tenantId)
    {
        // Public client with loopback redirect for interactive sign-in.
        _app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    // Sync entry point: block on the async path.
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var scopes = requestContext.Scopes;
        AuthenticationResult result;
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        try
        {
            // Silent acquisition from the MSAL cache.
            result = await _app
                .AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            // Fallback: interactive browser sign-in.
            result = await _app
                .AcquireTokenInteractive(scopes)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
