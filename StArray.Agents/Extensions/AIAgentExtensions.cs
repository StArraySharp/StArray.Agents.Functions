using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using StArray.Agents.Abstraction;

namespace StArray.Agents.Extensions;

public static class AIAgentExtensions
{
    extension(AIAgent aiAgent)
    {
        public static AIAgent CreateWithOpenAI(string apiKey,
            string model = "gpt-5.5",
            string apiUrl = "https://api.openai.com",
            ReasoningOptions? reasoningOptions = null,
            string? instruction = "",
            string? name = null, 
            ITools? tools = null)
        {
            throw new NotImplementedException();
        }
        
        public static AIAgent CreateWithAnthropic(string apiKey, string model = "claude-2", string? instruction = "",
            string? name = null, ITools? tools = null)
        {
            throw new NotImplementedException();
        }
        
        public static AIAgent CreateDeepSeek(string apiKey, string model = "deepseek-v4-pro", string? instruction = "",
            string? name = null, ITools? tools = null)
        {
            throw new NotImplementedException();
        }

        public async Task RunAsync(string message, Action<AgentResponse> callback, bool reasoning = false,
            AgentSession? session = null,
            CancellationToken cancellationToken = default)
        {
            AgentRunOptions options = new AgentRunOptions
            {
                AdditionalProperties = new AdditionalPropertiesDictionary()
            };
            options.AdditionalProperties["thinking"] = reasoning ? "enabled" : "disabled";
            AgentResponse response = await aiAgent.RunAsync(message, session, options, cancellationToken);
            callback(response);
        }
        
        /*public async Task RunStreamingAsync(string message, Action<AgentResponse> callback, bool reasoning = false,
            AgentSession? session = null,
            CancellationToken cancellationToken = default)
        {
            AgentRunOptions options = new AgentRunOptions
            {
                AdditionalProperties = new AdditionalPropertiesDictionary()
            };
            options.AdditionalProperties["thinking"] = reasoning ? "enabled" : "disabled";
            var response = await aiAgent.RunStreamingAsync(message, session, options, cancellationToken);
            callback(response);
        }*/
    }
    
}