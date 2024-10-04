// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;

namespace PubsubImitateChat.Message;

public class GossipMessage(GossipMessage.MessageTypeEnum messageType, byte[] body)
{
    public enum MessageTypeEnum : byte
    {
        Hello = 0x00,
        Peers = 0x01,
        Subscribe =  0x03,
        UnSubscribe =  0x04,
        Data =  0x99,
    }

    public GossipMessage(byte[] bytes) : this((MessageTypeEnum)bytes[0], bytes[1..])
    {
    }

    public MessageTypeEnum MessageType { get; } = messageType;

    protected internal byte[] Body { get; } = body;

    public byte[] Serialize()
    {
        var ba = new List<byte> { (byte)MessageType };
        ba.AddRange(Body);
        return ba.ToArray();
    }
}
