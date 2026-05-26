using AnalyticsFrontendAPI;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("ANALYTICS_FRONTEND_ENDPOINT")
        ?? "http://localhost:8080");

var client = new CollaborationClient(endpoint);

Console.WriteLine($"AnalyticsFrontend CollaborationClient initialized for {endpoint}.");

try
{
    var response = await client.GetGetsAsync(null, null);
    Console.WriteLine($"GET / -> HTTP {response.Status}");
    Console.WriteLine(response.Content);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
}
