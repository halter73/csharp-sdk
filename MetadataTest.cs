using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Core.Tests
{
    // Test class-level attributes
    [Description("Test class for demonstrating metadata functionality")]
    [Category("TestCategory")]
    public class MetadataTestClass
    {
        // Test method-level attributes
        [McpServerTool]
        [Description("Test tool method")]
        [Category("ToolCategory")]
        public static string TestTool(string input) => $"Processed: {input}";

        [McpServerResource(UriTemplate = "test://resource/{id}")]
        [Description("Test resource method")]
        [Category("ResourceCategory")]
        public static string TestResource(string id) => $"Resource: {id}";

        [McpServerPrompt]
        [Description("Test prompt method")]
        [Category("PromptCategory")]
        public static string TestPrompt(string input) => $"Prompt: {input}";
    }

    public class MetadataTests
    {
        public static void TestMetadata()
        {
            // Test Tool Metadata
            var toolMethod = typeof(MetadataTestClass).GetMethod(nameof(MetadataTestClass.TestTool))!;
            var tool = McpServerTool.Create(toolMethod);
            
            Console.WriteLine("Tool Metadata:");
            foreach (var attr in tool.Metadata)
            {
                Console.WriteLine($"  {attr.GetType().Name}: {attr}");
            }
            
            // Test Resource Metadata
            var resourceMethod = typeof(MetadataTestClass).GetMethod(nameof(MetadataTestClass.TestResource))!;
            var resource = McpServerResource.Create(resourceMethod);
            
            Console.WriteLine("Resource Metadata:");
            foreach (var attr in resource.Metadata)
            {
                Console.WriteLine($"  {attr.GetType().Name}: {attr}");
            }
            
            // Test Prompt Metadata
            var promptMethod = typeof(MetadataTestClass).GetMethod(nameof(MetadataTestClass.TestPrompt))!;
            var prompt = McpServerPrompt.Create(promptMethod);
            
            Console.WriteLine("Prompt Metadata:");
            foreach (var attr in prompt.Metadata)
            {
                Console.WriteLine($"  {attr.GetType().Name}: {attr}");
            }
        }
    }
}
