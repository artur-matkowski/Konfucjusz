using Microsoft.Extensions.Options;

/// <summary>
/// Root configuration options for captcha providers.
/// </summary>
public class CaptchaOptions
{
    /// <summary>
    /// Active captcha provider identifier (e.g. "hcaptcha").
    /// </summary>
    public string Provider { get; set; } = "hcaptcha";

    /// <summary>
    /// hCaptcha-specific configuration.
    /// </summary>
    public HcaptchaOptions Hcaptcha { get; set; } = new();
}

/// <summary>
/// Configuration options for hCaptcha.
/// </summary>
public class HcaptchaOptions
{
    /// <summary>
    /// Public site key used on the frontend widget.
    /// </summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret key used for server-side verification.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}

/// <summary>
/// Abstraction for captcha verification.
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Verifies a captcha response token for a given client IP.
    /// </summary>
    /// <param name="token">Captcha response token from the client.</param>
    /// <param name="remoteIp">Optional client IP address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if verification succeeded; false otherwise.</returns>
    Task<bool> VerifyAsync(string token, string? remoteIp = null, CancellationToken ct = default);
}

/// <summary>
/// hCaptcha implementation of <see cref="ICaptchaVerifier"/>.
/// </summary>
public class HcaptchaVerifier : ICaptchaVerifier
{
    private readonly HttpClient _httpClient;
    private readonly HcaptchaOptions _options;

    private const string VerifyEndpoint = "https://hcaptcha.com/siteverify";

    private sealed class HcaptchaResponse
    {
        public bool Success { get; set; }
        public string[]? ErrorCodes { get; set; }
    }

    public HcaptchaVerifier(HttpClient httpClient, IOptions<CaptchaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.Hcaptcha ?? new HcaptchaOptions();
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(string token, string? remoteIp = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            throw new InvalidOperationException("hCaptcha secret is not configured. Check Captcha:hCaptcha:Secret in configuration.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var form = new Dictionary<string, string?>
        {
            ["secret"] = _options.Secret,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp;
        }

        using var content = new FormUrlEncodedContent(form!);

        var response = await _httpClient.PostAsync(VerifyEndpoint, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Treat network/HTTP errors as verification failure.
            return false;
        }

        var body = await response.Content.ReadFromJsonAsync<HcaptchaResponse>(cancellationToken: ct);
        return body?.Success ?? false;
    }
}
