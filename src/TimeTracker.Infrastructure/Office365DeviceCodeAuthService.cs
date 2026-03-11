using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using TimeTracker.Domain;

namespace TimeTracker.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class Office365DeviceCodeAuthService
{
    private const string Scope = "offline_access User.Read Calendars.Read";
    private static readonly HttpClient HttpClient = new();

    private readonly Office365TokenStore _tokenStore;

    public Office365DeviceCodeAuthService(Office365TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public async Task<string> GetAccessTokenAsync(
        Office365AccountSettings account,
        Action<string>? deviceCodePrompt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.TenantId);

        var cached = _tokenStore.Load(account);
        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return cached.AccessToken;
        }

        if (!string.IsNullOrWhiteSpace(cached?.RefreshToken))
        {
            var refreshed = await RefreshAsync(account, cached.RefreshToken!, cancellationToken);
            if (refreshed is not null)
            {
                _tokenStore.Save(account, refreshed);
                return refreshed.AccessToken;
            }
        }

        var deviceCode = await RequestDeviceCodeAsync(account, cancellationToken);
        TryOpenBrowser(deviceCode.VerificationUri);
        deviceCodePrompt?.Invoke(deviceCode.Message);
        var interactive = await PollForTokenAsync(account, deviceCode, cancellationToken);
        _tokenStore.Save(account, interactive);
        return interactive.AccessToken;
    }

    private static async Task<DeviceCodeResponse> RequestDeviceCodeAsync(
        Office365AccountSettings account,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.PostAsync(
            $"https://login.microsoftonline.com/{account.TenantId}/oauth2/v2.0/devicecode",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = account.ClientId,
                ["scope"] = Scope,
            }),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<DeviceCodeResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Device code request returned no payload.");
        return payload;
    }

    private static async Task<Office365TokenRecord> PollForTokenAsync(
        Office365AccountSettings account,
        DeviceCodeResponse deviceCode,
        CancellationToken cancellationToken)
    {
        var intervalSeconds = Math.Max(5, deviceCode.Interval);
        var expiry = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        while (DateTimeOffset.UtcNow < expiry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);

            using var response = await HttpClient.PostAsync(
                $"https://login.microsoftonline.com/{account.TenantId}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = account.ClientId,
                    ["device_code"] = deviceCode.DeviceCode,
                }),
                cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Token response returned no payload.");

            if (response.IsSuccessStatusCode)
            {
                return ToTokenRecord(payload);
            }

            if (payload.Error is "authorization_pending" or "slow_down")
            {
                if (payload.Error == "slow_down")
                {
                    intervalSeconds += 5;
                }

                continue;
            }

            throw new InvalidOperationException(payload.ErrorDescription ?? "Office 365 sign-in failed.");
        }

        throw new TimeoutException("Office 365 sign-in expired before authorization completed.");
    }

    private static async Task<Office365TokenRecord?> RefreshAsync(
        Office365AccountSettings account,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.PostAsync(
            $"https://login.microsoftonline.com/{account.TenantId}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = account.ClientId,
                ["refresh_token"] = refreshToken,
                ["scope"] = Scope,
            }),
            cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return null;
        }

        return ToTokenRecord(payload);
    }

    private static Office365TokenRecord ToTokenRecord(TokenResponse payload)
    {
        return new Office365TokenRecord
        {
            AccessToken = payload.AccessToken ?? throw new InvalidOperationException("Missing access token."),
            RefreshToken = payload.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, payload.ExpiresIn)),
        };
    }

    private static void TryOpenBrowser(string verificationUri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = verificationUri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Browser launch failure should not stop the device-code flow.
        }
    }

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
