using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Protocols.Grpc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/grpc/schemas")]
public class ProtoSchemasController(IProtoSchemaRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await repo.GetAllAsync(ct);
        return Ok(new { items, total = items.Count });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var schema = await repo.GetByIdAsync(id, ct);
        return schema is null ? NotFound() : Ok(schema);
    }

    // 1 MB is far larger than any real .proto file; bigger uploads are almost
    // certainly attacker-supplied or malformed.
    private const int MaxProtoBytes = 1 * 1024 * 1024;

    /// <summary>
    /// Upload a .proto file as plain text. The server parses field mappings and stores them.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxProtoBytes)]
    [Consumes("application/x-protobuf-schema", "text/plain", "application/octet-stream")]
    public async Task<IActionResult> Upload(
        [FromQuery] string? name,
        CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var protoText = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(protoText))
            return BadRequest("Request body must contain .proto file content.");
        if (protoText.Length > MaxProtoBytes)
            return BadRequest($"Proto content exceeds {MaxProtoBytes} byte limit.");

        var flatMap = ProtoParser.ParseFlatMap(protoText);
        var schema = new ProtoSchema
        {
            Name = name ?? "Unnamed schema",
            RawProto = protoText,
            FieldMapJson = ProtoParser.ToJson(flatMap)
        };

        await repo.AddAsync(schema, ct);
        return Created($"/api/grpc/schemas/{schema.Id}", schema);
    }

    /// <summary>
    /// Upload a .proto file as JSON body for clients that prefer JSON.
    /// </summary>
    [HttpPost("json")]
    [RequestSizeLimit(MaxProtoBytes)]
    public async Task<IActionResult> UploadJson([FromBody] UploadProtoDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ProtoText))
            return BadRequest("ProtoText is required.");
        if (dto.ProtoText.Length > MaxProtoBytes)
            return BadRequest($"Proto content exceeds {MaxProtoBytes} byte limit.");

        var flatMap = ProtoParser.ParseFlatMap(dto.ProtoText);
        var schema = new ProtoSchema
        {
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Unnamed schema" : dto.Name,
            RawProto = dto.ProtoText,
            FieldMapJson = ProtoParser.ToJson(flatMap)
        };

        await repo.AddAsync(schema, ct);
        return Created($"/api/grpc/schemas/{schema.Id}", schema);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();
        await repo.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record UploadProtoDto(string? Name, string ProtoText);
