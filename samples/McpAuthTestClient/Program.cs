using ModelContextProtocol.Client;
using System.Net.Http.Headers;

async Task TestWithAuth(string? token, string description)
{
    Console.WriteLine($"\n=== {description} ===");

    // Create an HTTP client to test the MCP server
    using var httpClient = new HttpClient();

    // Add JWT token if provided
    if (!string.IsNullOrEmpty(token))
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Console.WriteLine($"Using JWT token: {token[..Math.Min(20, token.Length)]}...");
    }
    else
    {
        Console.WriteLine("No authentication token (anonymous access)");
    }

    // Test the MCP server running on localhost:3001
    var transport = new SseClientTransport(new()
    {
        Endpoint = new Uri("http://localhost:3001"),
    }, httpClient);

    try
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(transport);

        // Test listing tools - should show authorization filtering
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing tools: {ex.Message}");
        }

        // Test calling an anonymous tool (Echo)
        Console.WriteLine("\n2. Testing Anonymous Tool (Echo):");
        try
        {
            var result = await mcpClient.CallToolAsync("Echo", new Dictionary<string, object?>
            {
                ["message"] = "test message"
            });

            if (result.IsError == true)
            {
                Console.WriteLine($"Error: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
            else
            {
                Console.WriteLine($"Success: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Echo: {ex.Message}");
        }

        // Test calling a protected tool (ProtectedEcho)
        Console.WriteLine("\n3. Testing Protected Tool (ProtectedEcho):");
        try
        {
            var result = await mcpClient.CallToolAsync("ProtectedEcho", new Dictionary<string, object?>
            {
                ["message"] = "protected message"
            });

            if (result.IsError == true)
            {
                Console.WriteLine($"Error: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
            else
            {
                Console.WriteLine($"Success: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling ProtectedEcho: {ex.Message}");
        }

        // Test calling an admin-only tool (sampleLLM)
        Console.WriteLine("\n4. Testing Admin-Only Tool (sampleLLM):");
        try
        {
            var result = await mcpClient.CallToolAsync("sampleLLM", new Dictionary<string, object?>
            {
                ["prompt"] = "test prompt",
                ["maxTokens"] = 100
            });

            if (result.IsError == true)
            {
                Console.WriteLine($"Error: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
            else
            {
                Console.WriteLine($"Success: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling sampleLLM: {ex.Message}");
        }

        // Test calling the simple LLM tool
        Console.WriteLine("\n5. Testing Simple LLM Tool (Anonymous):");
        try
        {
            var result = await mcpClient.CallToolAsync("SimpleLLM", new Dictionary<string, object?>
            {
                ["message"] = "simple test"
            });

            if (result.IsError == true)
            {
                Console.WriteLine($"Error: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
            else
            {
                Console.WriteLine($"Success: {string.Join(", ", result.Content?.Select(c => c.ToString()) ?? [])}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling SimpleLLM: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to connect to MCP server: {ex.Message}");
    }
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
