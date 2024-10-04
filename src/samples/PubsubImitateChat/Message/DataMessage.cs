// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;

namespace PubsubImitateChat.Message;

public class DataMessage : GossipMessage
{
    public string Topic;
    public string Content;
    public DataMessage(string topic, string content) : base(MessageTypeEnum.Data, Serialize(topic, content))
    {
        Topic = topic;
        Content = content;
    }

    public DataMessage(GossipMessage message) : base(MessageTypeEnum.Data, message.Body)
    {
        var bytes = message.Body;
        var topicLength = BitConverter.ToInt32(bytes[..4]);
        Topic = Encoding.UTF8.GetString(bytes[4..(4 + topicLength)]);
        Content = Encoding.UTF8.GetString(bytes[(4 + topicLength)..]);
    }

    private static byte[] Serialize(string topic, string content)
    {
        var topicSerialized = Encoding.UTF8.GetBytes(topic);
        IEnumerable<byte> ba = BitConverter.GetBytes(topicSerialized.Length);
        ba = ba.Concat(topicSerialized);
        ba = ba.Concat(Encoding.UTF8.GetBytes(content));
        return ba.ToArray();
    }
}
