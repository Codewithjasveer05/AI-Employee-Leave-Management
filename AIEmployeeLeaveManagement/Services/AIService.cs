using System.Text;
using System.Text.Json;

namespace AIEmployeeLeaveManagement.Services
{
    public class AIService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string[] _models;

        // how many times to try each model when Google is busy (503/429/500)
        private const int MaxAttempts = 3;

        public AIService(HttpClient http, IConfiguration config)
        {
            _http = http;

            // Trim() removes hidden spaces/new lines copied along with the key
            _apiKey = (config["Gemini:ApiKey"] ?? "").Trim().Trim('"');

            _models = (config["Gemini:Models"] ??
                "gemini-3.5-flash,gemini-3.1-flash-lite,gemini-3-flash-preview,gemini-2.5-flash-lite,gemini-2.5-flash")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Sends a prompt to Google Gemini and returns the answer
        public async Task<string> GetSuggestionAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.StartsWith("PASTE"))
            {
                return "AI key is not set. Please add the Gemini API key in appsettings.json.";
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };
            string json = JsonSerializer.Serialize(requestBody);

            var errors = new List<string>();

            foreach (var model in _models)
            {
                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post,
                            "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent");
                        request.Headers.Add("x-goog-api-key", _apiKey);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await _http.SendAsync(request);
                        var result = await response.Content.ReadAsStringAsync();
                        int code = (int)response.StatusCode;

                        if (!response.IsSuccessStatusCode)
                        {
                            // wrong key: no point trying other models or retrying
                            if (code == 400 && result.Contains("API key not valid"))
                            {
                                return "Your Gemini API key is not valid. Please copy the FULL key from " +
                                       "https://aistudio.google.com/apikey and paste it in appsettings.json, then restart the app.";
                            }

                            bool busy = code == 503 || code == 429 || code == 500;

                            if (busy && attempt < MaxAttempts)
                            {
                                // Google is busy: wait 2s, then 4s, and try the same model again
                                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                                continue;
                            }

                            errors.Add(model + " returned " + code + ": " + GetApiErrorMessage(result));
                            break; // go to next model
                        }

                        string? text = ReadText(result);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }

                        errors.Add(model + " gave an empty response");
                        break; // go to next model
                    }
                    catch (Exception ex)
                    {
                        if (attempt < MaxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                            continue;
                        }
                        errors.Add(model + ": " + ex.Message);
                    }
                }
            }

            return "AI is not available right now. Please try again in a minute. " + string.Join(" | ", errors);
        }

        // Reads all text parts from the Gemini response
        private static string? ReadText(string result)
        {
            using var doc = JsonDocument.Parse(result);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            if (candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var t))
                    {
                        sb.Append(t.GetString());
                    }
                }
            }
            return sb.ToString();
        }

        // Reads the real error message that Google sends back
        private static string GetApiErrorMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                {
                    var m = msg.GetString() ?? "";
                    return m.Length > 200 ? m.Substring(0, 200) + "..." : m;
                }
            }
            catch { }
            return body.Length > 200 ? body.Substring(0, 200) + "..." : body;
        }
    }
}