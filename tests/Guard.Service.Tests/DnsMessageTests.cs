using System.Net;
using AdultContentShutdownGuard.Guard.Service.Services;
using Xunit;

namespace Guard.Service.Tests;

public sealed class DnsMessageTests
{
    [Fact]
    public void TryParseQuery_extracts_question_name_and_transaction_id()
    {
        var query = DnsTestPacket.BuildAQuery("blocked.example", 0x1234);

        var parsed = DnsMessage.TryParseQuery(query, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal(0x1234, message.TransactionId);
        Assert.Equal("blocked.example", message.QuestionName);
        Assert.Equal(DnsRecordType.A, message.QuestionType);
    }

    [Fact]
    public void CreateSinkholeResponse_returns_a_record_for_blocked_query()
    {
        var query = DnsTestPacket.BuildAQuery("blocked.example", 0xCAFE);
        Assert.True(DnsMessage.TryParseQuery(query, out var message));

        var response = message!.CreateSinkholeResponse(IPAddress.Parse("0.0.0.0"));

        Assert.Equal(0xCA, response[0]);
        Assert.Equal(0xFE, response[1]);
        Assert.Equal(0x81, response[2]);
        Assert.Equal(0x80, response[3]);
        Assert.Equal(0x00, response[6]);
        Assert.Equal(0x01, response[7]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, response[^4..]);
    }

    private static class DnsTestPacket
    {
        public static byte[] BuildAQuery(string host, ushort transactionId)
        {
            var bytes = new List<byte>
            {
                (byte)(transactionId >> 8),
                (byte)(transactionId & 0xFF),
                0x01,
                0x00,
                0x00,
                0x01,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            };

            foreach (var label in host.Split('.'))
            {
                bytes.Add((byte)label.Length);
                bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
            }

            bytes.Add(0);
            bytes.Add(0);
            bytes.Add((byte)DnsRecordType.A);
            bytes.Add(0);
            bytes.Add(1);
            return bytes.ToArray();
        }
    }
}
