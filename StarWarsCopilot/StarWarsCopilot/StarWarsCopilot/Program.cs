using StarWarsCopilot;
using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.Inference;
using ModelContextProtocol.Client;

using ChatRole = Microsoft.Extensions.AI.ChatRole;

// Create a logger factory
var factory = LoggerFactory.Create(builder => builder.AddConsole()
                                                     .SetMinimumLevel(LogLevel.Trace));

// Create the IChatClient
var client = new AzureOpenAIClient(new Uri(LLMOptions.Endpoint),
                                   new ApiKeyCredential(LLMOptions.ApiKey));

var innerClient = client.GetChatClient(LLMOptions.Model).AsIChatClient();

// var innerClient = new ChatCompletionsClient(new Uri(LLMOptions.AIInferenceEndpoint),
//                                             new AzureKeyCredential(LLMOptions.ApiKey))
//                                             .AsIChatClient(LLMOptions.AIInferenceModel);

var chatClient = new ChatClientBuilder(innerClient)
                    .UseLogging(factory)
                    .UseFunctionInvocation() // enables tool calling
                    .Build();

var clientTransport = new StdioClientTransport(new()
{
    Name = MCPServerOptions.Name,
    Command = MCPServerOptions.Command,
    Arguments = MCPServerOptions.Arguments,
}, loggerFactory: factory);

await using var mcpClient = await McpClient.CreateAsync(clientTransport,
                                                        loggerFactory: factory);

IList<AITool> tools = [..await mcpClient.ListToolsAsync()];
ChatOptions options = new() { Tools = [..tools] };

// Create a history store the conversation
//var succintStylePrompt = @"
//        Always respond with the most succinct answer possible.
//        For example, reply with one word or a short phrase when appropriate.";

var yodaStylePrompt = @"
You are a helpful assistant that provides information about Star Wars.
Always respond in the style of Yoda, the wise Jedi Master.
Give warnings about paths to the dark side.
If the user says hello there, then only respond with General Kenobi! and nothing else.
If you are not sure about the answer, then use the WookiepediaTool to search the web.";

//var tedLassoStylePrompt = @"You are a helpful assistant that provides guidance and information.
//Always respond in the style of Ted Lasso, the football coach for AFC Richmond.
//";

var history = new List<ChatMessage>
{
    new(ChatRole.System, yodaStylePrompt)
};


// Initiate a back-and-forth chat
while (true)
{
  // Collect user input
  Console.Write("User > ");
  var userInput = Console.ReadLine();

  // End processing if user input is null or empty
  if (string.IsNullOrWhiteSpace(userInput))
    break;

  // Add user input to the chat history
  history.Add(new ChatMessage(ChatRole.User, userInput));

  // Get the response from the AI
  var result = await chatClient.GetResponseAsync(history, options);

  // Add the AI response to the chat history
  history.Add(new ChatMessage(ChatRole.Assistant, result.Messages.Last()?.Text ?? string.Empty));

  // Print the results
  Console.WriteLine("Assistant > " + result.Messages.Last()?.Text);
}