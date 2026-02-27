using IoTSpy.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificates")]
public class CertificatesController(
    ICertificateAuthority ca,
    ICertificateRepository certs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await certs.GetAllAsync());

    [HttpGet("root-ca")]
    public async Task<IActionResult> GetRootCa()
    {
        var entry = await ca.GetOrCreateRootCaAsync();
        return Ok(new
        {
            entry.Id,
            entry.CommonName,
            entry.SerialNumber,
            entry.NotBefore,
            entry.NotAfter,
            entry.IsRootCa,
            entry.CreatedAt,
            // Don't expose private key in listing
            CertificatePem = entry.CertificatePem
        });
    }

    [HttpGet("root-ca/download")]
    [AllowAnonymous] // Allow download without auth for easy device setup
    public async Task<IActionResult> DownloadRootCaDer()
    {
        var der = await ca.ExportRootCaDerAsync();
        return File(der, "application/x-x509-ca-cert", "iotspy-ca.crt");
    }

    [HttpGet("root-ca/pem")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadRootCaPem()
    {
        var entry = await ca.GetOrCreateRootCaAsync();
        return File(
            System.Text.Encoding.UTF8.GetBytes(entry.CertificatePem),
            "application/x-pem-file",
            "iotspy-ca.pem");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await certs.DeleteAsync(id);
        return NoContent();
    }
}
