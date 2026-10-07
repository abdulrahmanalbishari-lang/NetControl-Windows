using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NetControl.Windows;

public sealed class NetControlApi
{
    private readonly HttpClient _http;
    public string BaseUrl { get; set; } = "";
    public string Token { get; private set; } = "";

    public NetControlApi()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    private async Task<JsonDocument> Request(HttpMethod method, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(method, BaseUrl.TrimEnd('/') + path);
        if (!string.IsNullOrWhiteSpace(Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        if (body is not null)
            req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req);
        var txt = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)res.StatusCode}: {txt}");

        return JsonDocument.Parse(txt);
    }

    public async Task<JsonDocument> Login(string email, string password)
    {
        var r = await Request(HttpMethod.Post, "/api/auth/login", new { email, password });
        if (r.RootElement.TryGetProperty("token", out var t))
            Token = t.GetString() ?? "";
        return r;
    }

    public Task<JsonDocument> Me() => Request(HttpMethod.Get, "/api/me");
    public Task<JsonDocument> Plans() => Request(HttpMethod.Get, "/api/plans");
    public Task<JsonDocument> Devices() => Request(HttpMethod.Get, "/api/devices");
    public Task<JsonDocument> CreateDevice(object body) => Request(HttpMethod.Post, "/api/devices", body);
    public Task<JsonDocument> RemoteAccess() => Request(HttpMethod.Get, "/api/remote-access");
    public Task<JsonDocument> ProgrammingAccess() => Request(HttpMethod.Get, "/api/programming/access");
}
