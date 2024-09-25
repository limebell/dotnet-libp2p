// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;
using Multiformats.Address;

namespace PubsubImitateChat.Message;

public class PeersMessage : GossipMessage
{
    public IReadOnlyCollection<Multiaddress> Peers;
    public PeersMessage(IReadOnlyCollection<Multiaddress> peers) : base(MessageTypeEnum.Peers, SerializePeers(peers))
    {
        Peers = peers;
    }

    public PeersMessage(GossipMessage message) : base(MessageTypeEnum.Peers, message.Body)
    {
        Peers = DeserializePeers(message.Body);
    }

    private static byte[] SerializePeers(IReadOnlyCollection<Multiaddress> peers)
    {
        var ba = BitConverter.GetBytes(peers.Count).ToList();
        foreach (Multiaddress address in peers)
        {
            var peerSerialized = Encoding.UTF8.GetBytes(address.ToString());
            var length = peerSerialized.Length;
            ba.AddRange(BitConverter.GetBytes(length));
            ba.AddRange(peerSerialized);
        }

        return ba.ToArray();
    }

    private IReadOnlyCollection<Multiaddress> DeserializePeers(byte[] bytes)
    {
        List<Multiaddress> peers = new();
        var count = BitConverter.ToInt32(bytes[..4]);
        var index = 4;
        for (int i = 0; i < count; i++)
        {
            var peerLength = BitConverter.ToInt32(bytes[index..(index + 4)]);
            peers.Add(Multiaddress.Decode(Encoding.UTF8.GetString(bytes[(index + 4)..(index + 4 + peerLength)])));
            index = index + 4 + peerLength;
        }

        return peers;
    }
}
