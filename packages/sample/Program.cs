using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsFrontendAPI;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Identity.Client;

return await CliApp.RunAsync(args);

internal static class CliApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        var parsed = CliArgs.Parse(args);

        if (parsed.ShowHelp || parsed.Verb is null)
        {
            PrintHelp();
            return parsed.Verb is null && !parsed.ShowHelp ? 1 : 0;
        }

        var client = BuildClient(parsed);

        try
        {
            var response = await DispatchAsync(client, parsed);
            Console.WriteLine($"HTTP {response.Status} {response.ReasonPhrase}");
            if (response.Content is not null)
            {
                var body = response.Content.ToString();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    Console.WriteLine(body);
                }
            }
            return response.Status >= 400 ? response.Status : 0;
        }
        catch (RequestFailedException ex)
        {
            Console.Error.WriteLine($"Request failed (HTTP {ex.Status}): {ex.Message}");
            return ex.Status == 0 ? 1 : ex.Status;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            PrintHelp();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static CollaborationClient BuildClient(CliArgs parsed)
    {
        var endpoint = new Uri(
            parsed.Endpoint
                ?? Environment.GetEnvironmentVariable("ANALYTICS_FRONTEND_ENDPOINT")
                ?? "http://localhost:8080");

        var scope = Environment.GetEnvironmentVariable("ANALYTICS_FRONTEND_SCOPE")
            ?? "https://management.azure.com/.default";

        TokenCredential credential = parsed.UseMsal
            ? new MsalTokenCredential(
                clientId: Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
                    ?? throw new InvalidOperationException("AZURE_CLIENT_ID must be set when using --use-msal."),
                tenantId: Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "organizations")
            : new DefaultAzureCredential();

        var options = new CollaborationClientOptions();

        if (parsed.InsecureTls)
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

        return new CollaborationClient(endpoint, options);
    }

    private static Task<Response> DispatchAsync(CollaborationClient c, CliArgs a)
    {
        // Verb routing table: maps verb tokens to SDK calls.
        // Style: <resource> <action> [positional args...]
        return (a.Verb, a.SubVerb) switch
        {
            ("collaborations", "list") =>
                c.GetGetsAsync(a.GetBool("--include-deleted"), null),
            ("collaborations", "get") =>
                c.IdGetAsync(a.Required(0, "collaborationId"), a.GetBool("--include-deleted"), null),
            ("collaborations", "report") =>
                c.ReportGetAsync(a.Required(0, "collaborationId"), null),

            ("analytics", "get") =>
                c.AnalyticsGetAsync(a.Required(0, "collaborationId"), null),
            ("analytics", "skr-policy") =>
                c.AnalyticsSkrPolicyGetAsync(a.Required(0, "collaborationId"), a.Required(1, "kid"), null),

            ("oidc", "issuer-info") =>
                c.OidcIssuerInfoGetAsync(a.Required(0, "collaborationId"), null),
            ("oidc", "set-issuer-url") =>
                c.OidcSetIssuerUrlPostAsync(a.Required(0, "collaborationId"), a.RequireBody(), null),
            ("oidc", "keys") =>
                c.OidcKeysGetAsync(a.Required(0, "collaborationId"), null),

            ("invitations", "list") =>
                c.InvitationsGetAsync(a.Required(0, "collaborationId"), a.GetBool("--include-deleted"), null),
            ("invitations", "get") =>
                c.InvitationIdGetAsync(a.Required(0, "collaborationId"), a.Required(1, "invitationId"), null),
            ("invitations", "accept") =>
                c.InvitationIdAcceptPostAsync(a.Required(0, "collaborationId"), a.Required(1, "invitationId"), null),

            ("datasets", "list") =>
                c.AnalyticsDatasetsListGetAsync(a.Required(0, "collaborationId"), null),
            ("datasets", "get") =>
                c.AnalyticsDatasetsDocumentIdGetAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), null),
            ("datasets", "publish") =>
                c.AnalyticsDatasetsDocumentIdPublishPostAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), a.RequireBody(), null),
            ("datasets", "queries") =>
                c.AnalyticsDatasetsDocumentIdQueriesGetAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), null),

            ("consent", "get") =>
                c.ConsentDocumentIdGetAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), null),
            ("consent", "put") =>
                c.ConsentDocumentIdPutAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), a.RequireBody(), null),

            ("queries", "list") =>
                c.AnalyticsQueriesListGetAsync(a.Required(0, "collaborationId"), null),
            ("queries", "get") =>
                c.AnalyticsQueriesDocumentIdGetAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), null),
            ("queries", "publish") =>
                c.AnalyticsQueriesDocumentIdPublishPostAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), a.RequireBody(), null),
            ("queries", "vote") =>
                c.AnalyticsQueriesDocumentIdVotePostAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), a.RequireBody(), null),
            ("queries", "run") =>
                c.AnalyticsQueriesDocumentIdRunPostAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), a.RequireBody(), null),
            ("queries", "runs") =>
                c.AnalyticsQueriesDocumentIdRunsGetAsync(a.Required(0, "collaborationId"), a.Required(1, "documentId"), null),

            ("runs", "get") =>
                c.AnalyticsRunsJobIdGetAsync(a.Required(0, "collaborationId"), a.Required(1, "jobId"), null),

            ("secrets", "put") =>
                c.AnalyticsSecretsSecretNamePutAsync(a.Required(0, "collaborationId"), a.Required(1, "secretName"), a.RequireBody(), null),

            ("audit-events", "list") =>
                c.AnalyticsAuditeventsGetAsync(
                    a.Required(0, "collaborationId"),
                    a.GetOption("--from"),
                    a.GetOption("--to"),
                    a.GetOption("--type"),
                    null),

            _ => throw new ArgumentException($"Unknown verb: '{a.Verb} {a.SubVerb}'.")
        };
    }

    private static void PrintHelp()
    {
        var help = """
            Azure Cleanroom Analytics Frontend - CLI

            Usage:
              afe [global-options] <resource> <action> [args...] [options]

            Global options:
              --endpoint <url>        Frontend base URL (env: ANALYTICS_FRONTEND_ENDPOINT)
              --use-msal              Use MSAL interactive auth instead of DefaultAzureCredential
              --insecure              Skip TLS certificate validation (dev/test only)
              --body <json|@file>     Request body as inline JSON or @path/to/file.json
              -h | --help             Show this help

            Resource/action verbs:
              collaborations list                              [--include-deleted]
              collaborations get      <collaborationId>        [--include-deleted]
              collaborations report   <collaborationId>

              analytics      get         <collaborationId>
              analytics      skr-policy  <collaborationId> <kid>

              oidc           issuer-info     <collaborationId>
              oidc           set-issuer-url  <collaborationId>  --body <json|@file>
              oidc           keys            <collaborationId>

              invitations    list    <collaborationId>             [--include-deleted]
              invitations    get     <collaborationId> <invitationId>
              invitations    accept  <collaborationId> <invitationId>

              datasets       list     <collaborationId>
              datasets       get      <collaborationId> <documentId>
              datasets       publish  <collaborationId> <documentId>  --body <json|@file>
              datasets       queries  <collaborationId> <documentId>

              consent        get      <collaborationId> <documentId>
              consent        put      <collaborationId> <documentId>  --body <json|@file>

              queries        list     <collaborationId>
              queries        get      <collaborationId> <documentId>
              queries        publish  <collaborationId> <documentId>  --body <json|@file>
              queries        vote     <collaborationId> <documentId>  --body <json|@file>
              queries        run      <collaborationId> <documentId>  --body <json|@file>
              queries        runs     <collaborationId> <documentId>

              runs           get      <collaborationId> <jobId>

              secrets        put      <collaborationId> <secretName>  --body <json|@file>

              audit-events   list     <collaborationId>  [--from <iso>] [--to <iso>] [--type <type>]

            Environment:
              ANALYTICS_FRONTEND_ENDPOINT  Default frontend URL
              ANALYTICS_FRONTEND_SCOPE     AAD scope (default: https://management.azure.com/.default)
              AZURE_CLIENT_ID              Required for --use-msal
              AZURE_TENANT_ID              Optional for --use-msal (default: organizations)
            """;
        Console.WriteLine(help);
    }
}

internal sealed class CliArgs
{
    public string? Verb { get; private set; }
    public string? SubVerb { get; private set; }
    public bool UseMsal { get; private set; }
    public bool InsecureTls { get; private set; }
    public bool ShowHelp { get; private set; }
    public string? Endpoint { get; private set; }
    public string? Body { get; private set; }

    private readonly List<string> _positional = new();
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public static CliArgs Parse(string[] args)
    {
        var r = new CliArgs();
        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];
            switch (token.ToLowerInvariant())
            {
                case "-h":
                case "--help":
                    r.ShowHelp = true;
                    break;
                case "--use-msal":
                    r.UseMsal = true;
                    break;
                case "--insecure":
                    r.InsecureTls = true;
                    break;
                case "--endpoint":
                    r.Endpoint = RequireValue(args, ref i, token);
                    break;
                case "--body":
                    r.Body = RequireValue(args, ref i, token);
                    break;
                default:
                    if (token.StartsWith("--", StringComparison.Ordinal))
                    {
                        // Generic option: --flag value or --flag (boolean)
                        string? value = null;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        {
                            // peek: only consume next as value if it doesn't look like the start of positional/verb after we already have a verb
                            // To stay simple, consume only when verb is set and we're past the sub-verb
                            if (r.Verb is not null && r.SubVerb is not null)
                            {
                                value = args[++i];
                            }
                        }
                        r._options[token] = value;
                    }
                    else if (r.Verb is null)
                    {
                        r.Verb = token;
                    }
                    else if (r.SubVerb is null)
                    {
                        r.SubVerb = token;
                    }
                    else
                    {
                        r._positional.Add(token);
                    }
                    break;
            }
        }
        return r;
    }

    private static string RequireValue(string[] args, ref int i, string token)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Option {token} requires a value.");
        }
        return args[++i];
    }

    public string Required(int index, string name)
    {
        if (index >= _positional.Count)
        {
            throw new ArgumentException($"Missing required argument <{name}>.");
        }
        return _positional[index];
    }

    public string? GetOption(string name) =>
        _options.TryGetValue(name, out var v) ? v : null;

    public bool GetBool(string name) => _options.ContainsKey(name);

    public RequestContent RequireBody()
    {
        if (string.IsNullOrEmpty(Body))
        {
            throw new ArgumentException("This verb requires --body <json|@file>.");
        }

        string json = Body.StartsWith("@", StringComparison.Ordinal)
            ? File.ReadAllText(Body.Substring(1))
            : Body;

        return RequestContent.Create(Encoding.UTF8.GetBytes(json));
    }
}

// Adapter exposing MSAL as an Azure.Core TokenCredential.
internal sealed class MsalTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _app;

    public MsalTokenCredential(string clientId, string tenantId)
    {
        _app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithRedirectUri("http://localhost")
            .Build();
    }

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
            result = await _app
                .AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            result = await _app
                .AcquireTokenInteractive(scopes)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
