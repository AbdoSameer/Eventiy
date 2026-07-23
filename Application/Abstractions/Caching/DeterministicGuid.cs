using System.Security.Cryptography;
using System.Text;

namespace Application.Abstractions.Caching;

public static class DeterministicGuid
{
    private static readonly Guid StripeEventNamespace = new("a91b6a53-cad9-4f9c-8c8e-68b4e2f1c9a0");

    public static Guid FromStripeEventId(string stripeEventId)
    {
        return CreateVersion5(StripeEventNamespace, stripeEventId);
    }

    public static Guid CreateVersion5(Guid namespaceGuid, string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        return CreateVersion5(namespaceGuid, nameBytes);
    }

    public static Guid CreateVersion5(Guid namespaceGuid, byte[] nameBytes)
    {
        var namespaceBytes = namespaceGuid.ToByteArray();
        SwapEndianness(namespaceBytes);

        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, buffer, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, buffer, namespaceBytes.Length, nameBytes.Length);

        var hash = SHA1.HashData(buffer);

        var guidBytes = new byte[16];
        Buffer.BlockCopy(hash, 0, guidBytes, 0, 16);

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[7] = (byte)((guidBytes[7] & 0x3F) | 0x80);

        SwapEndianness(guidBytes);

        return new Guid(guidBytes);
    }

    private static void SwapEndianness(byte[] guid)
    {
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }

    private static void Swap(byte[] bytes, int i, int j)
    {
        (bytes[i], bytes[j]) = (bytes[j], bytes[i]);
    }
}
