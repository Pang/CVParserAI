using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CVParserAI;

class Program
{
    private static readonly string _filePath = "C:\\Users\\JohnDoe\\Documents\\TestCV.txt";

    static async Task Main(string[] args)
    {
        Console.WriteLine("-- BEGIN --\n");

        var request = CreatePrompt();
        var httpResponse = await PostPromptToAI(request);
        var textResponse = await StreamOutputFromServer(httpResponse);
        Resume resume = FindAndConvertJson(textResponse);

        Console.WriteLine("\nFull name from the JSON: " + resume.FullName);

        Console.WriteLine("\n\n--- FIN ---");
        Console.ReadLine();
    }

    private static object CreatePrompt()
    {
        string resumeText = File.ReadAllText(_filePath);

        string prompt = @"
            You are an expert CV parser. Extract the following from the resume:
            - Full Name
            - Email
            - Phone Number
            - Skills

            Return the results exactly as you seen in the following format:";

        prompt += "```{";
        prompt += "\"fullName\" : \"John Doe\",";
        prompt += "\"email\" : \"john@example.com\",";
        prompt += "\"phoneNumber\" : \"123-456-7890\",";
        prompt += "\"skills\": {";
        prompt += "\"languages\": [\"C#\", \"JavaScript\"],";
        prompt += "\"frameworks\": [\"ASP.NET Core\", \"React\"]";
        prompt += "\"toolsAndPlatforms\": [\"Azure\", \"Git\"]";
        prompt += "\"databases\": [\"SQL Server\"]";
        prompt += "\"practices\": [\"Agile\", \"TDD\"]";
        prompt += "}";
        prompt += "}```";

        prompt += "Resume: ";
        prompt += "\n";
        prompt += resumeText;
        prompt += "\n";

        var request = new
        {
            model = "llama3",
            prompt = prompt
        };

        return request;
    }

    // Post the prompt to the AI server
    private static async Task<HttpResponseMessage> PostPromptToAI(object request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate");
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        message.Content = content;

        var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
        return response;
    }

    // Turn response into a stream so we don't need to wait for the full text.
    private static async Task<string> StreamOutputFromServer(HttpResponseMessage? response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var fullResponse = "";
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                try
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(line);
                    if (json.TryGetProperty("response", out var token))
                    {
                        Console.Write(token.GetString());
                        fullResponse += token;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[JSON Error] {ex.Message}");
                }
            }
        }
        Console.WriteLine("\n\n");
        return fullResponse;
    }

    // Find and extract the JSON from the response, then convert it.
    private static Resume FindAndConvertJson(string response)
    {
        // Find everything between backticks
        var match = Regex.Match(response, "```(.*?)```", RegexOptions.Singleline);
        string extractedJson = "";

        if (match.Success)
        {
            extractedJson = match.Groups[1].Value.Trim();
            Console.WriteLine("Extracted JSON:");
            Console.WriteLine(extractedJson);
        }
        else
        {
            Console.WriteLine("No content found between triple backticks.");
        }

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        Resume resume = JsonSerializer.Deserialize<Resume>(extractedJson, jsonSerializerOptions);
        return resume;
    }
}
