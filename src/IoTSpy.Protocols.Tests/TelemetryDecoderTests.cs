using System.Text;
using Xunit;
using IoTSpy.Protocols.Telemetry;

namespace IoTSpy.Protocols.Tests;

public class TelemetryDecoderTests
{
    // ── DatadogDecoder ────────────────────────────────────────────────────────

    public class DatadogDecoderTests
    {
        private readonly DatadogDecoder _decoder = new();

        [Fact]
        public void CanDecode_SeriesPayload_ReturnsTrue()
        {
            var json = """{"series":[{"metric":"cpu","points":[[1700000000,42.5]],"host":"web01"}]}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_LogsPayload_ReturnsTrue()
        {
            var json = """{"logs":[{"message":"hello","hostname":"web01"}]}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_UnrelatedJson_ReturnsFalse()
        {
            var json = """{"foo":"bar","baz":123}""";
            Assert.False(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_Empty_ReturnsFalse()
        {
            Assert.False(_decoder.CanDecode([]));
        }

        [Fact]
        public async Task DecodeAsync_SeriesPayload_ExtractsMetricAndHost()
        {
            var json = """
                {
                  "series": [
                    {
                      "metric": "system.cpu.user",
                      "points": [[1700000000, 75.3]],
                      "host": "prod-web-01",
                      "type": "gauge",
                      "tags": ["env:production", "region:us-east-1"]
                    }
                  ]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.Datadog, msg.Protocol);
            Assert.Equal("prod-web-01", msg.Source);
            Assert.Equal(1, msg.EventCount);
            Assert.Equal("system.cpu.user", msg.Events[0]["metric"]);
            Assert.Equal("prod-web-01", msg.Events[0]["host"]);
            Assert.Equal("gauge", msg.Events[0]["type"]);
            Assert.NotNull(msg.Timestamp);
        }

        [Fact]
        public async Task DecodeAsync_MultipleSeries_AllExtracted()
        {
            var json = """
                {
                  "series": [
                    { "metric": "cpu", "points": [[1700000000, 50.0]], "host": "h1" },
                    { "metric": "mem", "points": [[1700000001, 80.0]], "host": "h1" },
                    { "metric": "disk", "points": [[1700000002, 30.0]], "host": "h1" }
                  ]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            Assert.Equal(3, messages[0].EventCount);
        }

        [Fact]
        public async Task DecodeAsync_LogsPayload_ExtractsFields()
        {
            var json = """
                {
                  "logs": [
                    {
                      "message": "request received",
                      "hostname": "api-01",
                      "service": "auth",
                      "timestamp": "2023-11-14T10:00:00Z"
                    }
                  ]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.Datadog, msg.Protocol);
            Assert.Equal("api-01", msg.Source);
            Assert.Equal("request received", msg.Events[0]["message"]);
            Assert.Equal("auth", msg.Events[0]["service"]);
        }

        [Fact]
        public async Task DecodeAsync_MalformedJson_ReturnsEmpty()
        {
            var messages = await _decoder.DecodeAsync("not json at all"u8.ToArray());
            Assert.Empty(messages);
        }

        [Fact]
        public async Task DecodeAsync_EmptyBody_ReturnsEmpty()
        {
            var messages = await _decoder.DecodeAsync(""u8.ToArray());
            Assert.Empty(messages);
        }
    }

    // ── FirehoseDecoder ───────────────────────────────────────────────────────

    public class FirehoseDecoderTests
    {
        private readonly FirehoseDecoder _decoder = new();

        [Fact]
        public void CanDecode_ValidFirehosePayload_ReturnsTrue()
        {
            var json = """{"requestId":"abc","timestamp":1700000000,"records":[{"data":"aGVsbG8="}]}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_NoRecordsKey_ReturnsFalse()
        {
            var json = """{"requestId":"abc","timestamp":1700000000}""";
            Assert.False(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public async Task DecodeAsync_Base64Records_DecodesData()
        {
            // base64 of "hello world"
            var base64 = Convert.ToBase64String("hello world"u8.ToArray());
            var json = $$"""
                {
                  "requestId": "req-001",
                  "timestamp": 1700000000000,
                  "records": [
                    { "data": "{{base64}}" }
                  ]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.AwsFirehose, msg.Protocol);
            Assert.Equal(1, msg.EventCount);
            Assert.Equal("hello world", msg.Events[0]["data_text"]);
        }

        [Fact]
        public async Task DecodeAsync_Base64JsonRecord_FlattensNestedJson()
        {
            var innerJson = """{"temperature":22.5,"device":"sensor-01","unit":"celsius"}""";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(innerJson));
            var json = $$"""
                {
                  "requestId": "req-002",
                  "timestamp": 1700000000000,
                  "records": [{ "data": "{{base64}}" }]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            Assert.Equal(TelemetryProtocol.AwsFirehose, messages[0].Protocol);
            Assert.True(messages[0].Events[0].ContainsKey("temperature"));
            Assert.Equal("sensor-01", messages[0].Events[0]["device"]);
        }

        [Fact]
        public async Task DecodeAsync_MultipleRecords_AllDecoded()
        {
            var r1 = Convert.ToBase64String("record1"u8.ToArray());
            var r2 = Convert.ToBase64String("record2"u8.ToArray());
            var json = $$"""
                {
                  "requestId": "req-003",
                  "timestamp": 1700000000000,
                  "records": [{"data":"{{r1}}"},{"data":"{{r2}}"}]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            Assert.Equal(2, messages[0].EventCount);
        }

        [Fact]
        public async Task DecodeAsync_MalformedJson_ReturnsEmpty()
        {
            var messages = await _decoder.DecodeAsync("{bad json"u8.ToArray());
            Assert.Empty(messages);
        }
    }

    // ── SplunkHecDecoder ──────────────────────────────────────────────────────

    public class SplunkHecDecoderTests
    {
        private readonly SplunkHecDecoder _decoder = new();

        [Fact]
        public void CanDecode_SingleEventWithHost_ReturnsTrue()
        {
            var json = """{"time":1700000000.0,"host":"web01","event":{"msg":"ok"}}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_EventWithSourcetype_ReturnsTrue()
        {
            var json = """{"time":1700000000.0,"sourcetype":"syslog","event":"something happened"}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_NoEventKey_ReturnsFalse()
        {
            var json = """{"host":"web01","source":"app"}""";
            Assert.False(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public async Task DecodeAsync_SingleEvent_ExtractsFields()
        {
            var json = """
                {"time":1700000000.123,"host":"splunk-host","source":"/var/log/app","sourcetype":"json","event":{"level":"ERROR","message":"disk full","code":500}}
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.SplunkHec, msg.Protocol);
            Assert.Equal("splunk-host", msg.Source);
            Assert.NotNull(msg.Timestamp);
            Assert.Equal("ERROR", msg.Events[0]["event.level"]);
            Assert.Equal("disk full", msg.Events[0]["event.message"]);
        }

        [Fact]
        public async Task DecodeAsync_BatchedEvents_AllDecoded()
        {
            var batch = string.Join("\n",
                """{"time":1700000001.0,"host":"h1","event":"event one"}""",
                """{"time":1700000002.0,"host":"h1","event":"event two"}""",
                """{"time":1700000003.0,"host":"h1","event":"event three"}"""
            );

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(batch));

            Assert.Single(messages);
            Assert.Equal(3, messages[0].EventCount);
            Assert.Equal(TelemetryProtocol.SplunkHec, messages[0].Protocol);
        }

        [Fact]
        public async Task DecodeAsync_StringEvent_StoredAsSingleField()
        {
            var json = """{"host":"h1","sourcetype":"plain","event":"a plain text log line"}""";

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            Assert.Equal("a plain text log line", messages[0].Events[0]["event"]);
        }

        [Fact]
        public async Task DecodeAsync_MalformedJson_SkipsLine()
        {
            var batch = "not json\n{\"host\":\"h1\",\"event\":\"ok\",\"sourcetype\":\"t\"}";

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(batch));

            // Second line is valid
            Assert.Single(messages);
            Assert.Equal(1, messages[0].EventCount);
        }

        [Fact]
        public async Task DecodeAsync_EmptyBody_ReturnsEmpty()
        {
            var messages = await _decoder.DecodeAsync(""u8.ToArray());
            Assert.Empty(messages);
        }
    }

    // ── AzureMonitorDecoder ───────────────────────────────────────────────────

    public class AzureMonitorDecoderTests
    {
        private readonly AzureMonitorDecoder _decoder = new();

        [Fact]
        public void CanDecode_ArrayWithTimeGenerated_ReturnsTrue()
        {
            var json = """[{"TimeGenerated":"2023-11-14T10:00:00Z","Computer":"vm-01","Message":"boot"}]""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_DiagnosticLogFormat_ReturnsTrue()
        {
            var json = """{"resourceId":"/subscriptions/abc","operationName":"Write","category":"Policy"}""";
            Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public void CanDecode_UnrelatedJson_ReturnsFalse()
        {
            var json = """{"name":"test","value":42}""";
            Assert.False(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
        }

        [Fact]
        public async Task DecodeAsync_ArrayPayload_ExtractsEntries()
        {
            var json = """
                [
                  {
                    "TimeGenerated": "2023-11-14T10:00:00Z",
                    "Computer": "vm-prod-01",
                    "EventID": 4625,
                    "Message": "Failed logon attempt"
                  },
                  {
                    "TimeGenerated": "2023-11-14T10:01:00Z",
                    "Computer": "vm-prod-01",
                    "EventID": 4624,
                    "Message": "Successful logon"
                  }
                ]
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.AzureMonitor, msg.Protocol);
            Assert.Equal(2, msg.EventCount);
            Assert.NotNull(msg.Timestamp);
            Assert.Equal("Failed logon attempt", msg.Events[0]["Message"]);
        }

        [Fact]
        public async Task DecodeAsync_RecordsWrapper_ExtractsEntries()
        {
            var json = """
                {
                  "records": [
                    {
                      "TimeGenerated": "2023-11-14T11:00:00Z",
                      "resourceId": "/subscriptions/abc/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm01",
                      "operationName": "Microsoft.Compute/virtualMachines/write",
                      "category": "Administrative",
                      "properties": { "statusCode": "Created", "serviceRequestId": "req-123" }
                    }
                  ]
                }
                """;

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            var msg = messages[0];
            Assert.Equal(TelemetryProtocol.AzureMonitor, msg.Protocol);
            Assert.Equal(1, msg.EventCount);
            // resourceId last segment becomes Source
            Assert.Equal("vm01", msg.Source);
            // Nested properties should be flattened
            Assert.Equal("Created", msg.Events[0]["properties.statusCode"]);
        }

        [Fact]
        public async Task DecodeAsync_SingleObjectWithTimeGenerated_Decoded()
        {
            var json = """{"TimeGenerated":"2023-11-14T12:00:00Z","Level":"Warning","Message":"disk usage high"}""";

            var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(json));

            Assert.Single(messages);
            Assert.Equal(1, messages[0].EventCount);
            Assert.Equal("Warning", messages[0].Events[0]["Level"]);
        }

        [Fact]
        public async Task DecodeAsync_MalformedJson_ReturnsEmpty()
        {
            var messages = await _decoder.DecodeAsync("{broken"u8.ToArray());
            Assert.Empty(messages);
        }
    }
}
