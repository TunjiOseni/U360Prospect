
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using U360Prospect.Configuration;

namespace U360Prospect.Services;

public sealed class EntrustTokenService
{
    private static readonly XNamespace SoapNamespace =
        "http://schemas.xmlsoap.org/soap/envelope/";

    private static readonly XNamespace EntrustNamespace =
        "http://ws.waei.uba.com/";

    private readonly HttpClient _httpClient;
    private readonly EntrustSettings _settings;
    private readonly ILogger<EntrustTokenService> _logger;

    public EntrustTokenService(
        HttpClient httpClient,
        IOptions<EntrustSettings> options,
        ILogger<EntrustTokenService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }
public async Task<bool> ValidateTokenAsync(
    string username,
    string tokenCode,
    string? requesterIp = null,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(_settings.ServiceUrl) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(tokenCode))
    {
        _logger.LogWarning(
            "Entrust verification cannot run because the service URL or required input is missing.");

        return false;
    }

    try
    {
        var envelope = new XDocument(
            new XElement(SoapNamespace + "Envelope",
                new XAttribute(
                    XNamespace.Xmlns + "soapenv",
                    SoapNamespace),
                new XAttribute(
                    XNamespace.Xmlns + "ws",
                    EntrustNamespace),
                new XElement(SoapNamespace + "Header"),
                new XElement(
                    SoapNamespace + "Body",
                    new XElement(
                        EntrustNamespace + "authenticateToken",
                        new XElement(
                            "request",
                            new XElement("response", tokenCode),
                            new XElement("userGroup", _settings.UserGroup),
                            new XElement("username", username),
                            new XElement("requesterId", _settings.RequesterId),
                            new XElement(
                                "requesterIp",
                                requesterIp ?? string.Empty)
                        )
                    )
                )
            )
        );

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _settings.ServiceUrl);

        request.Content = new StringContent(
            envelope.ToString(SaveOptions.DisableFormatting),
            System.Text.Encoding.UTF8,
            "text/xml");

        request.Headers.TryAddWithoutValidation(
            "SOAPAction",
            "\"\"");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Entrust returned HTTP status code {StatusCode}.",
                (int)response.StatusCode);

            return false;
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1_000_000
            });

        var result = XDocument.Load(reader);

        var successValue = result
            .Descendants()
            .FirstOrDefault(
                e => e.Name.LocalName == "isSuccessful")
            ?.Value;

        var responseDetails = result
            .Descendants()
            .Where(e =>
                e.Name.LocalName != "response" &&
                e.Name.LocalName != "token")
            .Select(e => $"{e.Name.LocalName}={e.Value.Trim()}")
            .Where(x => !x.EndsWith("="))
            .ToList();

        _logger.LogWarning(
            "Entrust response: HTTP {StatusCode}, IsSuccessful={IsSuccessful}, Details={Details}",
            (int)response.StatusCode,
            successValue?.Trim(),
            string.Join(" | ", responseDetails));

        return string.Equals(
            successValue?.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }
    catch (OperationCanceledException)
        when (!cancellationToken.IsCancellationRequested)
    {
        _logger.LogWarning(
            "Entrust verification timed out.");

        return false;
    }
    catch (Exception ex)
    {
        // Do not log the request, token code, or SOAP payload.
        _logger.LogError(
            ex,
            "Entrust verification failed due to a service or response error.");

        return false;
    }

  }
}
