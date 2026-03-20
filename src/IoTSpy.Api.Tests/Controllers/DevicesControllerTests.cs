using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class DevicesControllerTests
{
    private static Device MakeDevice(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        IpAddress = "192.168.1.100",
        Label = "Test Device"
    };

    [Fact]
    public async Task List_ReturnsAllDevices()
    {
        var repo = Substitute.For<IDeviceRepository>();
        repo.GetAllAsync().Returns(new List<Device> { MakeDevice(), MakeDevice() });

        var controller = new DevicesController(repo);
        var result = await controller.List() as OkObjectResult;

        Assert.NotNull(result);
        var devices = Assert.IsType<List<Device>>(result.Value);
        Assert.Equal(2, devices.Count);
    }

    [Fact]
    public async Task Get_WhenFound_ReturnsDevice()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IDeviceRepository>();
        repo.GetByIdAsync(id).Returns(MakeDevice(id));

        var controller = new DevicesController(repo);
        var result = await controller.Get(id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<Device>(result.Value);
    }

    [Fact]
    public async Task Get_WhenNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<IDeviceRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Device?)null);

        var controller = new DevicesController(repo);
        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenFound_PatchesFieldsAndReturnsDevice()
    {
        var id = Guid.NewGuid();
        var device = MakeDevice(id);
        var repo = Substitute.For<IDeviceRepository>();
        repo.GetByIdAsync(id).Returns(device);
        repo.UpdateAsync(Arg.Any<Device>()).Returns(device);

        var controller = new DevicesController(repo);
        var result = await controller.Update(id, new DevicePatchDto("New Label", "Notes", true)) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal("New Label", device.Label);
        Assert.Equal("Notes", device.Notes);
        Assert.True(device.InterceptionEnabled);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<IDeviceRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Device?)null);

        var controller = new DevicesController(repo);
        var result = await controller.Update(Guid.NewGuid(), new DevicePatchDto(null, null, null));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_CallsRepoAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IDeviceRepository>();
        repo.DeleteAsync(id).Returns(Task.CompletedTask);

        var controller = new DevicesController(repo);
        var result = await controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteAsync(id);
    }

    [Fact]
    public async Task Update_WithNullPatch_DoesNotOverrideExistingValues()
    {
        var id = Guid.NewGuid();
        var device = MakeDevice(id);
        device.Label = "Original Label";
        device.Notes = "Original Notes";

        var repo = Substitute.For<IDeviceRepository>();
        repo.GetByIdAsync(id).Returns(device);
        repo.UpdateAsync(Arg.Any<Device>()).Returns(device);

        var controller = new DevicesController(repo);
        await controller.Update(id, new DevicePatchDto(null, null, null));

        Assert.Equal("Original Label", device.Label);
        Assert.Equal("Original Notes", device.Notes);
    }
}
