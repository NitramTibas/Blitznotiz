using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Blitztext;

public sealed class OpenAiException : Exception
{
    public OpenAiException(string message) : base(message) { }
}

/// <summary>
/// Spricht die OpenAI-kompatible REST-Schnittstelle: Transkription und Chat-Completion.
/// Basis-Adresse konfigurierbar (OpenAI direkt oder eigener Zbox-Proxy).
/// </summary>
public sealed class OpenAiClient
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            // Warme Verbindung zwischen sporadischen Diktaten offen halten (Default 1 min ist zu kurz).
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
            // Endlich statt Infinite, damit ein DNS-/Adresswechsel irgendwann erkannt wird.
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.ExpectContinue = false; // versteckten 100-continue-Roundtrip ausschließen
        return client;
    }

    private readonly string _baseUrl;
    private readonly string _token;

    public OpenAiClient(string baseUrl, string token)
    {
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com" : baseUrl.TrimEnd('/');
        _token = token;
    }

    /// <summary>Öffnet vorab die Verbindung (DNS + TCP + TLS), damit der echte POST den Handschlag spart.
    /// Fire-and-forget, Fehler werden bewusst verschluckt – darf den Aufnahme-/Verarbeitungspfad nie stören.</summary>
    public void WarmUp()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl);
                using var response = await Http.SendAsync(request, cts.Token).ConfigureAwait(false);
                await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            }
            catch { /* Vorwärmen ist reiner Bonus */ }
        });
    }

    public async Task<string> TranscribeAsync(string wavPath, string model, string? prompt, string? language, CancellationToken ct)
    {
        var selectedModel = string.IsNullOrWhiteSpace(model) ? Workflows.TranscriptionModel : model.Trim();
        try
        {
            return await TranscribeOnceAsync(wavPath, selectedModel, prompt, language, ct).ConfigureAwait(false);
        }
        catch (OpenAiException ex) when (
            !selectedModel.Equals(Workflows.FallbackTranscriptionModel, StringComparison.OrdinalIgnoreCase) &&
            LooksLikeModelCompatibilityError(ex.Message))
        {
            Logger.Log($"Transkriptionsmodell {selectedModel} nicht kompatibel, Fallback auf {Workflows.FallbackTranscriptionModel}.");
            return await TranscribeOnceAsync(wavPath, Workflows.FallbackTranscriptionModel, prompt, language, ct).ConfigureAwait(false);
        }
    }

    private async Task<string> TranscribeOnceAsync(string wavPath, string model, string? prompt, string? language, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        await using var audioStream = new FileStream(
            wavPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var fileContent = new StreamContent(audioStream, 64 * 1024);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "audio.wav");
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent("text"), "response_format");
        if (!string.IsNullOrWhiteSpace(prompt)) form.Add(new StringContent(prompt), "prompt");
        if (!string.IsNullOrWhiteSpace(language)) form.Add(new StringContent(language), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Content = form;

        var body = await SendAsync(request, TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
        return body.Trim();
    }

    public async Task<string> ChatAsync(string model, string systemPrompt, string userText, double temperature, CancellationToken ct)
    {
        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userText }
            },
            temperature,
            max_tokens = 900
        };
        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var body = await SendAsync(request, TimeSpan.FromSeconds(45), ct).ConfigureAwait(false);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return (content ?? "").Trim();
        }
        catch (Exception ex)
        {
            throw new OpenAiException("Antwort konnte nicht gelesen werden: " + ex.Message);
        }
    }

    private static async Task<string> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new OpenAiException(ExtractError(body, response.StatusCode));
            return body;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OpenAiException("Zeitüberschreitung – Server hat nicht rechtzeitig geantwortet.");
        }
        catch (HttpRequestException ex)
        {
            throw new OpenAiException("Netzwerkfehler: " + ex.Message);
        }
    }

    private static string ExtractError(string body, HttpStatusCode code)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                return $"{(int)code}: {msg.GetString()}";
        }
        catch { /* kein JSON */ }

        var snippet = body.Length > 200 ? body[..200] : body;
        return $"HTTP {(int)code}: {snippet}";
    }

    private static bool LooksLikeModelCompatibilityError(string message) =>
        message.Contains("model", StringComparison.OrdinalIgnoreCase)
        || message.Contains("response_format", StringComparison.OrdinalIgnoreCase)
        || message.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
        || message.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
