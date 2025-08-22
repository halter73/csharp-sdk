using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

/// <summary>
/// This tool uses dependency injection and async method
/// </summary>
[McpServerToolType]
public sealed class SampleLlmTool
{
    [McpServerTool(Name = "sampleLLM"), Description("Samples from an LLM using MCP's sampling feature")]
    [Authorize(Roles = "Admin")] // This tool requires Admin role
    public static async Task<string> SampleLLM(
        IMcpServer thisServer,
        [Description("The prompt to send to the LLM")] string prompt,
        [Description("Maximum number of tokens to generate")] int maxTokens,
        CancellationToken cancellationToken)
    {
        ChatOptions options = new()
        {
            Instructions = "You are a helpful test server.",
            MaxOutputTokens = maxTokens,
            Temperature = 0.7f,
        };

        //var samplingResponse = await thisServer.AsSamplingChatClient().GetResponseAsync(prompt, options, cancellationToken);

        return $"LLM sampling result: this is fake, but you must have been an admin to see this.";
    }

    [McpServerTool, Description("A simple LLM tool that allows anonymous access")]
    [AllowAnonymous]
    public static string SimpleLLM([Description("The input message")] string message)
    {
        return $"Simple LLM response: {message}";
    }
}
