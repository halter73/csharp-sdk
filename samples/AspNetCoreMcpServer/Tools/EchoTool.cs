using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public sealed class EchoTool
{
    [McpServerTool, Description("Echoes the input back to the client.")]
    [AllowAnonymous] // This tool allows anonymous access
    public static string Echo(string message)
    {
        return "hello " + message;
    }

    [McpServerTool, Description("A protected echo that requires authorization.")]
    [Authorize] // This tool requires authorization
    public static string ProtectedEcho(string message)
    {
        return "Protected: " + message;
    }
}
