using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificates")]
public class CertificatesController(
    ICertificateAuthority ca,
    ICertificateRepository certs,
    IAuditRepository auditRepo) : ControllerBase
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

    [HttpPost("root-ca/regenerate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RegenerateRootCa()
    {
        var all = await certs.GetAllAsync();
        foreach (var cert in all)
            await certs.DeleteAsync(cert.Id);

        var newCa = await ca.GetOrCreateRootCaAsync();

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "RegenerateRootCA",
            EntityType = "CertificateEntry",
            EntityId = newCa?.Id.ToString(),
            Details = "Regenerated root CA and purged all leaf certificates",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { message = "Root CA regenerated", commonName = newCa?.CommonName, id = newCa?.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await certs.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Deletes all leaf (non-CA) certificates so they are regenerated on next use.</summary>
    [HttpDelete("purge-leaf-certs")]
    public async Task<IActionResult> PurgeLeafCerts()
    {
        var all = await certs.GetAllAsync();
        var leafCerts = all.Where(c => !c.IsRootCa).ToList();
        foreach (var cert in leafCerts)
            await certs.DeleteAsync(cert.Id);
        return Ok(new { deleted = leafCerts.Count });
    }
}
