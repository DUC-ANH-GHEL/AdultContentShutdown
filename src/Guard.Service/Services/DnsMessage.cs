using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public enum DnsRecordType : ushort
{
    A = 1,
    Aaaa = 28
}

public sealed class DnsMessage
{
    private readonly byte[] _queryBytes;
    private readonly int _questionEndOffset;

    private DnsMessage(byte[] queryBytes, ushort transactionId, string questionName, DnsRecordType questionType, int questionEndOffset)
    {
        _queryBytes = queryBytes;
        TransactionId = transactionId;
        QuestionName = questionName;
        QuestionType = questionType;
        _questionEndOffset = questionEndOffset;
    }

    public ushort TransactionId { get; }

    public string QuestionName { get; }

    public DnsRecordType QuestionType { get; }

    public static bool TryParseQuery(ReadOnlySpan<byte> queryBytes, out DnsMessage? message)
    {
        message = null;
        if (queryBytes.Length < 17)
        {
            return false;
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(queryBytes[4..6]);
        if (questionCount != 1)
        {
            return false;
        }

        var offset = 12;
        var labels = new List<string>();
        while (offset < queryBytes.Length)
        {
            var labelLength = queryBytes[offset++];
            if (labelLength == 0)
            {
                break;
            }

            if ((labelLength & 0xC0) != 0 || offset + labelLength > queryBytes.Length)
            {
                return false;
            }

            labels.Add(Encoding.ASCII.GetString(queryBytes.Slice(offset, labelLength)));
            offset += labelLength;
        }

        if (labels.Count == 0 || offset + 4 > queryBytes.Length)
        {
            return false;
        }

        var questionType = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(queryBytes.Slice(offset, 2));
        var transactionId = BinaryPrimitives.ReadUInt16BigEndian(queryBytes[..2]);
        var questionEndOffset = offset + 4;
        message = new DnsMessage(queryBytes.ToArray(), transactionId, string.Join('.', labels).ToLowerInvariant(), questionType, questionEndOffset);
        return true;
    }

    public byte[] CreateSinkholeResponse(IPAddress sinkholeAddress)
    {
        if (QuestionType != DnsRecordType.A || sinkholeAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return CreateNxDomainResponse();
        }

        var response = new List<byte>();
        response.AddRange(_queryBytes[..2]);
        response.AddRange(new byte[] { 0x81, 0x80 });
        response.AddRange(new byte[] { 0x00, 0x01 });
        response.AddRange(new byte[] { 0x00, 0x01 });
        response.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        response.AddRange(_queryBytes[12.._questionEndOffset]);
        response.AddRange(new byte[] { 0xC0, 0x0C });
        response.AddRange(new byte[] { 0x00, 0x01 });
        response.AddRange(new byte[] { 0x00, 0x01 });
        response.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x3C });
        response.AddRange(new byte[] { 0x00, 0x04 });
        response.AddRange(sinkholeAddress.GetAddressBytes());
        return response.ToArray();
    }

    public byte[] CreateNxDomainResponse()
    {
        var response = new List<byte>();
        response.AddRange(_queryBytes[..2]);
        response.AddRange(new byte[] { 0x81, 0x83 });
        response.AddRange(new byte[] { 0x00, 0x01 });
        response.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        response.AddRange(_queryBytes[12.._questionEndOffset]);
        return response.ToArray();
    }
}
