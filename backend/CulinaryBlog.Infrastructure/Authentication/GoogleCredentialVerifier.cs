using CulinaryBlog.Application.Authentication.Commands;
using Google.Apis.Auth;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class GoogleAuthOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
}

public sealed class GoogleCredentialVerifier(
    IOptions<GoogleAuthOptions> options,
    IHttpClientFactory clients) : IGoogleCredentialVerifier
{
    public async Task<GoogleIdentity?> VerifyAsync(string? idToken, string? authorizationCode, string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var token = idToken;
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(authorizationCode))
            token = await ExchangeCodeAsync(authorizationCode, codeVerifier, cancellationToken);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(options.Value.ClientId)) return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(token,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [options.Value.ClientId] });
            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email) ||
                payload.EmailVerified != true) return null;
            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name ?? payload.Email, payload.Picture);
        }
        catch (InvalidJwtException) { return null; }
    }

    private async Task<string?> ExchangeCodeAsync(string code, string? codeVerifier, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret) ||
            string.IsNullOrWhiteSpace(config.RedirectUri) || string.IsNullOrWhiteSpace(codeVerifier)) return null;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = config.ClientId, ["client_secret"] = config.ClientSecret,
            ["redirect_uri"] = config.RedirectUri, ["code_verifier"] = codeVerifier, ["grant_type"] = "authorization_code"
        });
        HttpResponseMessage response;
        try { response = await clients.CreateClient("google-oauth").PostAsync("token", content, cancellationToken); }
        catch (HttpRequestException) { throw new ServiceUnavailableException("GOOGLE_UNAVAILABLE", "Google verification is unavailable."); }
        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<GoogleTokenError>(cancellationToken);
            throw new ExternalAuthenticationException("GOOGLE_TOKEN_EXCHANGE_FAILED",
                $"Google token endpoint returned HTTP {(int)response.StatusCode}: {Safe(error?.Error)} — {Safe(error?.ErrorDescription)}");
        }
        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        return payload?.IdToken;
        }
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("id_token")] string? IdToken);

    private sealed record GoogleTokenError(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value)
        ? "not provided"
        : value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 256)];
}
