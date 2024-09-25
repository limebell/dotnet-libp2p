// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;
using Multiformats.Address;

namespace PubsubImitateChat.Message;

public class WrappedMessage
{
    public WrappedMessage(Multiaddress listenerAddress, GossipMessage body)
    {
        ListenerAddress = listenerAddress;
        Body = body;
    }

    public WrappedMessage(byte[] bytes)
    {
        var addressLength = BitConverter.ToInt32(bytes[..4]);
        ListenerAddress = Encoding.UTF8.GetString(bytes[4..(4 + addressLength)]);
        Body = new GossipMessage(bytes[(4 + addressLength)..]);
    }

    public Multiaddress ListenerAddress { get; set; }

    public GossipMessage Body { get; set; }

    public byte[] Serialize()
    {
        var addressSerialized = Encoding.UTF8.GetBytes(ListenerAddress.ToString());
        IEnumerable<byte> ba = BitConverter.GetBytes(addressSerialized.Length);
        ba = ba.Concat(addressSerialized);
        ba = ba.Concat(Body.Serialize());
        return ba.ToArray();
    }
}
