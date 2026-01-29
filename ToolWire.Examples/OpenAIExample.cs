#pragma warning disable OPENAI001

using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using ToolWire.Providers.OpenAI;
using ToolWire.SampleTools;
using ToolWire.Tools;

namespace ToolWire.Examples
{
    public static class OpenAIExample
    {
        private static readonly ResponsesClient client;
        static OpenAIExample()
        {
            #region OpenAI Client Init
            var credential = new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(Environment.GetEnvironmentVariable("OPENAI_BASE_URL")!)
            };
            var openAi = new OpenAIClient(credential, options);
            client = openAi.GetResponsesClient("model");
            #endregion
        }
        public static async Task RunAsync()
        {
            var executor = new ToolExecutor();
            executor.Registry.RegisterAll<TimeTool>();

            var conversation = new List<ResponseItem>();
            Console.WriteLine("ToolWire + OpenAI demo (type 'exit' to quit)\n");

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input) || input == "exit") break;
                conversation.Add(ResponseItem.CreateUserMessageItem(input));

                while (true)
                {
                    var request = new CreateResponseOptions(conversation).UseToolWire(executor);

                    var response = (await client.CreateResponseAsync(request)).Value;
                    conversation.AddRange(response.OutputItems);

                    var toolCall = response.OutputItems.OfType<FunctionCallResponseItem>().FirstOrDefault();
                    if (toolCall == null)
                    {
                        var text = response.OutputItems.OfType<MessageResponseItem>()
                            .Last(m => m.Role == MessageRole.Assistant).Content[0].Text;

                        Console.WriteLine($"\n{text}\n");
                        break;
                    }

                    var toolResult = await executor.ExecuteAsync(toolCall);
                    conversation.Add(toolResult);
                }
            }
        }
    }
}
