using System.Text;
using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ProtoSchemasControllerTests
{
    private static ProtoSchema MakeSchema(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "TestSchema",
        RawProto = "syntax = \"proto3\"; message Foo { string bar = 1; }",
        FieldMapJson = "{\"Foo\":{\"1\":\"bar\"}}"
    };

    private const string SimpleProto = """
        syntax = "proto3";
        message Greeting {
          string text = 1;
          int32 priority = 2;
        }
        """;

    private static ProtoSchemasController MakeController(IProtoSchemaRepository? repo = null, string? body = null)
    {
        var controller = new ProtoSchemasController(repo ?? Substitute.For<IProtoSchemaRepository>());

        if (body is not null)
        {
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Body = new MemoryStream(Encoding.UTF8.GetBytes(body))
                    }
                }
            };
        }

        return controller;
    }

    [Fact]
    public async Task List_ReturnsAllSchemasWithTotal()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { MakeSchema(), MakeSchema() });

        var controller = MakeController(repo);
        var result = await controller.List(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"total\":2", json);
        Assert.Contains("\"items\"", json);
    }

    [Fact]
    public async Task Get_WhenFound_ReturnsSchema()
    {
        var schema = MakeSchema();
        var repo = Substitute.For<IProtoSchemaRepository>();
        repo.GetByIdAsync(schema.Id, Arg.Any<CancellationToken>()).Returns(schema);

        var controller = MakeController(repo);
        var result = await controller.Get(schema.Id, TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Same(schema, result.Value);
    }

    [Fact]
    public async Task Get_WhenMissing_ReturnsNotFound()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProtoSchema?)null);

        var controller = MakeController(repo);
        var result = await controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Upload_WithProtoBody_ParsesAndPersistsSchema()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo, body: SimpleProto);

        var result = await controller.Upload("Greeting", TestContext.Current.CancellationToken);

        Assert.IsType<CreatedResult>(result);
        await repo.Received(1).AddAsync(Arg.Is<ProtoSchema>(s =>
            s.Name == "Greeting" &&
            s.RawProto.Contains("Greeting") &&
            s.FieldMapJson.Contains("text") &&
            s.FieldMapJson.Contains("priority")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithEmptyBody_ReturnsBadRequest()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo, body: "");

        var result = await controller.Upload(null, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        await repo.DidNotReceive().AddAsync(Arg.Any<ProtoSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithoutName_DefaultsToUnnamedSchema()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo, body: SimpleProto);

        await controller.Upload(null, TestContext.Current.CancellationToken);

        await repo.Received(1).AddAsync(Arg.Is<ProtoSchema>(s => s.Name == "Unnamed schema"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadJson_HappyPath_PersistsSchema()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo);
        var dto = new UploadProtoDto("MySchema", SimpleProto);

        var result = await controller.UploadJson(dto, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedResult>(result);
        await repo.Received(1).AddAsync(Arg.Is<ProtoSchema>(s =>
            s.Name == "MySchema" && s.RawProto == SimpleProto),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadJson_WithEmptyText_ReturnsBadRequest()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo);
        var dto = new UploadProtoDto("Name", "");

        var result = await controller.UploadJson(dto, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        await repo.DidNotReceive().AddAsync(Arg.Any<ProtoSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadJson_WithBlankName_DefaultsToUnnamedSchema()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        var controller = MakeController(repo);
        var dto = new UploadProtoDto("   ", SimpleProto);

        await controller.UploadJson(dto, TestContext.Current.CancellationToken);

        await repo.Received(1).AddAsync(Arg.Is<ProtoSchema>(s => s.Name == "Unnamed schema"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenFound_ReturnsNoContent()
    {
        var schema = MakeSchema();
        var repo = Substitute.For<IProtoSchemaRepository>();
        repo.GetByIdAsync(schema.Id, Arg.Any<CancellationToken>()).Returns(schema);

        var controller = MakeController(repo);
        var result = await controller.Delete(schema.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteAsync(schema.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFoundAndDoesNotCallDelete()
    {
        var repo = Substitute.For<IProtoSchemaRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProtoSchema?)null);

        var controller = MakeController(repo);
        var result = await controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
