using Einsatzueberwachung.Domain.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Einsatzueberwachung.Server.Security;
using QRCoder;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class EinsatzMonitor
{
    [Inject] private ITeamMobileTokenService TeamMobileTokenService { get; set; } = default!;
    [Inject] private IOptionsMonitor<TeamMobileOptions> TeamMobileOptions { get; set; } = default!;

    private bool _showTeamMobileModal;
    private string _teamMobileQrDataUrl = string.Empty;
    private string _teamMobileFullUrl = string.Empty;

    private void OpenTeamMobileModal()
    {
        var token = TeamMobileTokenService.CurrentMasterToken;
        if (string.IsNullOrEmpty(token))
        {
            _teamMobileFullUrl = string.Empty;
            _teamMobileQrDataUrl = string.Empty;
        }
        else
        {
            var baseUrl = (TeamMobileOptions.CurrentValue.PublicBaseUrl ?? string.Empty).TrimEnd('/');
            _teamMobileFullUrl = string.IsNullOrEmpty(baseUrl)
                ? $"/team/login/{token}"
                : $"{baseUrl}/team/login/{token}";

            _teamMobileQrDataUrl = GenerateQrDataUrl(_teamMobileFullUrl);
        }
        _showTeamMobileModal = true;
    }

    private void CloseTeamMobileModal() => _showTeamMobileModal = false;

    private static string GenerateQrDataUrl(string content)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var pngQr = new PngByteQRCode(data);
        var bytes = pngQr.GetGraphic(8);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
