using Google.GenAI;
using Google.GenAI.Types;
using ToolWire.Providers.Google;
using ToolWire.SampleTools;
using ToolWire.Tools;

namespace ToolWire.Examples
{
    public static class GeminiExample
    {
        private static readonly Client client;

        static GeminiExample()
        {
            client = new Client(
                apiKey: System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")!
            );
        }

        public static async Task RunAsync()
        {
            var executor = new ToolExecutor();
            executor.Registry.RegisterAll<TimeTool>();

            Console.WriteLine("ToolWire + Gemini demo (type 'exit' to quit)\n");

            var contents = new List<Content>();

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input) || input == "exit")
                    break;

                contents.Add(new Content
                {
                    Role = "user",
                    Parts = new List<Part>
                    {
                        new Part { Text = input }
                    }
                });

                while (true)
                {
                    var response = await client.Models.GenerateContentAsync(
                        model: "gemini-2.5-flash-lite",
                        contents: contents,
                        config: new GenerateContentConfig
                        {
                            Tools = new List<Google.GenAI.Types.Tool>
                            {
                                executor.Registry.ToGeminiTool()
                            }
                        }
                    );

                    var candidate = response.Candidates[0];
                    contents.Add(candidate.Content);

                    var functionCall = candidate.Content.Parts
                        .FirstOrDefault(p => p.FunctionCall != null)
                        ?.FunctionCall;

                    if (functionCall == null)
                    {
                        var text = candidate.Content.Parts
                            .FirstOrDefault(p => p.Text != null)
                            ?.Text;

                        if (text != null)
                            Console.WriteLine($"\n{text}\n");

                        break;
                    }

                    var toolResultPart =
                        await executor.ExecuteAsync(functionCall);

                    contents.Add(new Content
                    {
                        Role = "tool",
                        Parts = new List<Part>
                        {
                            toolResultPart
                        }
                    });

                }
            }
        }
    }
}
