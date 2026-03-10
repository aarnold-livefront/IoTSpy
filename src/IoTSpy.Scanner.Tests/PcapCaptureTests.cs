using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Storage;
using IoTSpy.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IoTSpy.Scanner.Tests
{
    public class PcapCaptureTests : IDisposable
    {
        private readonly IoTSpyDbContext _context;
        private readonly ICaptureDeviceRepository _deviceRepo;
        private readonly IPacketRepository _packetRepo;

        public PcapCaptureTests()
        {
            var options = new DbContextOptionsBuilder<IoTSpyDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _context = new IoTSpyDbContext(options);
            _context.Database.EnsureCreated();

            _deviceRepo = new CaptureDeviceRepository(_context);
            _packetRepo = (IPacketRepository)_deviceRepo;
        }

        [Fact]
        public async Task AddCaptureDevice_ShouldSucceed()
        {
            // Arrange
            var device = new CaptureDevice
            {
                Id = Guid.NewGuid(),
                Name = "eth0",
                DisplayName = "Ethernet 0",
                IpAddress = "192.168.1.1",
                MacAddress = "AA:BB:CC:DD:EE:FF"
            };

            // Act
            await _deviceRepo.AddAsync(device);
            var result = await _deviceRepo.GetByIdAsync(device.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("eth0", result.Name);
        }

        [Fact]
        public async Task GetAllDevices_ShouldReturnAllCapturedDevices()
        {
            // Arrange
            var devices = new List<CaptureDevice>
            {
                new CaptureDevice { Id = Guid.NewGuid(), Name = "eth0", DisplayName = "Ethernet 0", IpAddress = "192.168.1.1" },
                new CaptureDevice { Id = Guid.NewGuid(), Name = "wlan0", DisplayName = "WiFi 0", IpAddress = "192.168.1.2" }
            };

            foreach (var device in devices)
                await _deviceRepo.AddAsync(device);

            // Act
            var result = await _deviceRepo.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task AddPacket_ShouldSucceed()
        {
            // Arrange
            var device = new CaptureDevice
            {
                Id = Guid.NewGuid(),
                Name = "eth0",
                DisplayName = "Ethernet 0",
                IpAddress = "192.168.1.1",
                MacAddress = "AA:BB:CC:DD:EE:FF"
            };
            await _deviceRepo.AddAsync(device);

            var packet = new CapturedPacket
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                Timestamp = DateTimeOffset.UtcNow,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.200",
                SourcePort = 12345,
                DestinationPort = 80,
                Protocol = "TCP",
                Length = 1500,
                PayloadPreview = "Hello"
            };

            // Act
            await _packetRepo.AddAsync(packet);
            var result = await _packetRepo.GetByDeviceIdAsync(device.Id);

            // Assert
            Assert.Single(result);
            Assert.Equal("TCP", result.First().Protocol);
        }

        [Fact]
        public async Task FilterPackets_ShouldReturnMatchingResults()
        {
            // Arrange
            var device = new CaptureDevice
            {
                Id = Guid.NewGuid(),
                Name = "eth0",
                DisplayName = "Ethernet 0",
                IpAddress = "192.168.1.1",
                MacAddress = "AA:BB:CC:DD:EE:FF"
            };
            await _deviceRepo.AddAsync(device);

            var packets = new[]
            {
                new CapturedPacket { Id = Guid.NewGuid(), DeviceId = device.Id, Timestamp = DateTimeOffset.UtcNow, SourceIp = "192.168.1.100", DestinationIp = "192.168.1.200", Protocol = "TCP" },
                new CapturedPacket { Id = Guid.NewGuid(), DeviceId = device.Id, Timestamp = DateTimeOffset.UtcNow, SourceIp = "10.0.0.1", DestinationIp = "10.0.0.2", Protocol = "UDP" }
            };

            foreach (var packet in packets)
                await _packetRepo.AddAsync(packet);

            // Act - Filter by protocol
            var tcpPackets = await _packetRepo.GetByProtocolAsync(device.Id, "TCP");

            // Assert
            Assert.Single(tcpPackets);
            Assert.Equal("TCP", tcpPackets.First().Protocol);
        }

        [Fact]
        public async Task DeletePacket_ShouldRemoveFromRepository()
        {
            // Arrange
            var device = new CaptureDevice
            {
                Id = Guid.NewGuid(),
                Name = "eth0",
                DisplayName = "Ethernet 0",
                IpAddress = "192.168.1.1",
                MacAddress = "AA:BB:CC:DD:EE:FF"
            };
            await _deviceRepo.AddAsync(device);

            var packet = new CapturedPacket
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                Timestamp = DateTimeOffset.UtcNow,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.200",
                Protocol = "TCP"
            };
            await _packetRepo.AddAsync(packet);

            // Act
            var packetRepo = _deviceRepo as CaptureDeviceRepository;
            await packetRepo?.DeletePacketAsync(packet.Id);
            var result = await ((IPacketRepository)_deviceRepo).GetByDeviceIdAsync(device.Id);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetPacketsByProtocol_ShouldFilterCorrectly()
        {
            // Arrange
            var device = new CaptureDevice
            {
                Id = Guid.NewGuid(),
                Name = "eth0",
                DisplayName = "Ethernet 0",
                IpAddress = "192.168.1.1",
                MacAddress = "AA:BB:CC:DD:EE:FF"
            };
            await _deviceRepo.AddAsync(device);

            var packets = new[]
            {
                new CapturedPacket { Id = Guid.NewGuid(), DeviceId = device.Id, Timestamp = DateTimeOffset.UtcNow, SourceIp = "192.168.1.100", DestinationIp = "192.168.1.200", Protocol = "TCP" },
                new CapturedPacket { Id = Guid.NewGuid(), DeviceId = device.Id, Timestamp = DateTimeOffset.UtcNow, SourceIp = "10.0.0.1", DestinationIp = "10.0.0.2", Protocol = "UDP" },
                new CapturedPacket { Id = Guid.NewGuid(), DeviceId = device.Id, Timestamp = DateTimeOffset.UtcNow, SourceIp = "172.16.0.1", DestinationIp = "172.16.0.2", Protocol = "TCP" }
            };

            foreach (var packet in packets)
                await _packetRepo.AddAsync(packet);

            // Act
            var tcpPackets = await _packetRepo.GetByProtocolAsync(device.Id, "TCP");

            // Assert
            Assert.Equal(2, tcpPackets.Count());
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
