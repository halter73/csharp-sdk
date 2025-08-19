using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net.Http.Headers;

async Task TestWithAuth(string? token, string description)
{
    Console.WriteLine($"\n=== {description} ===");

    using var httpClient = new HttpClient();
    if (!string.IsNullOrEmpty(token))
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Console.WriteLine($"Using JWT token: {token[..Math.Min(20, token.Length)]}...");
    }
    else
    {
        Console.WriteLine("No authentication token (anonymous access)");
    }

    var transport = new SseClientTransport(new() { Endpoint = new Uri("http://localhost:3001") }, httpClient);

    // Tool name constants must match the server's published names exactly
    const string ToolEcho = "echo";                  // AllowAnonymous
    const string ToolProtectedEcho = "protected_echo"; // [Authorize]
    const string ToolAdminSampleLlm = "sampleLLM";   // [Authorize(Roles="Admin")]
    const string ToolSimpleLlm = "simple_llm";       // AllowAnonymous

    try
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(transport);

        // 1. List tools
        Console.WriteLine("\n1. Testing List Tools (should show authorization filtering):");
        try
        {
            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"Available tools: {tools.Count}");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error listing tools: {ex.Message}"); }

        // 2. Anonymous echo
        Console.WriteLine("\n2. Testing Anonymous Tool (echo):");
        try
        {
            var result = await mcpClient.CallToolAsync(ToolEcho, new Dictionary<string, object?> { ["message"] = "test message" });
            Console.WriteLine(result.IsError == true ? $"Error: {FormatContent(result.Content)}" : $"Success: {FormatContent(result.Content)}");
        }
        catch (Exception ex) { Console.WriteLine($"Error calling echo: {ex.Message}"); }

        // 3. Protected echo
        Console.WriteLine("\n3. Testing Protected Tool (protected_echo):");
        try
        {
            var result = await mcpClient.CallToolAsync(ToolProtectedEcho, new Dictionary<string, object?> { ["message"] = "protected message" });
            Console.WriteLine(result.IsError == true ? $"Error: {FormatContent(result.Content)}" : $"Success: {FormatContent(result.Content)}");
        }
        catch (Exception ex) { Console.WriteLine($"Error calling protected_echo: {ex.Message}"); }

        // 4. Admin-only sample LLM
        Console.WriteLine("\n4. Testing Admin-Only Tool (sampleLLM):");
        try
        {
            var result = await mcpClient.CallToolAsync(ToolAdminSampleLlm, new Dictionary<string, object?> { ["prompt"] = "test prompt", ["maxTokens"] = 100 });
            Console.WriteLine(result.IsError == true ? $"Error: {FormatContent(result.Content)}" : $"Success: {FormatContent(result.Content)}");
        }
        catch (Exception ex) { Console.WriteLine($"Error calling sampleLLM: {ex.Message}"); }

        // 5. Simple LLM
        Console.WriteLine("\n5. Testing Simple LLM Tool (simple_llm, Anonymous):");
        try
        {
            var result = await mcpClient.CallToolAsync(ToolSimpleLlm, new Dictionary<string, object?> { ["message"] = "simple test" });
            Console.WriteLine(result.IsError == true ? $"Error: {FormatContent(result.Content)}" : $"Success: {FormatContent(result.Content)}");
        }
        catch (Exception ex) { Console.WriteLine($"Error calling simple_llm: {ex.Message}"); }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to connect to MCP server: {ex.Message}");
    }
}

// Helper to format returned content blocks
static string FormatContent(IEnumerable<ContentBlock>? blocks)
{
    if (blocks == null) return string.Empty;
    var parts = new List<string>();
    foreach (var b in blocks)
    {
        switch (b)
        {
            case TextContentBlock t:
                parts.Add(t.Text ?? string.Empty);
                break;
            default:
                parts.Add(b.GetType().Name);
                break;
        }
    }
    return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

Console.WriteLine("MCP Authorization Test Client");
Console.WriteLine("============================");

// Get tokens from command line arguments or prompt
string? adminToken = null;
string? userToken = null;

if (args.Length >= 2)
{
    adminToken = args[0];
    userToken = args[1];
}
else
{
    Console.WriteLine("Usage: McpAuthTestClient <admin-token> <user-token>");
    Console.WriteLine("Or run without arguments to test anonymous access only.");
    Console.WriteLine();
}

// Test anonymous access
await TestWithAuth(null, "Anonymous Access");

// Test user access if token provided
if (!string.IsNullOrEmpty(userToken))
{
    await TestWithAuth(userToken, "Regular User Access");
}

// Test admin access if token provided
if (!string.IsNullOrEmpty(adminToken))
{
    await TestWithAuth(adminToken, "Admin User Access");
}

Console.WriteLine("\n=== All tests completed! ===");
