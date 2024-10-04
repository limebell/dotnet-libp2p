// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;

namespace PubsubImitateChat.Message;

public class SubscribeMessage : GossipMessage
{
    public string Topic;
    public SubscribeMessage(string topic) : base(MessageTypeEnum.Subscribe, Encoding.UTF8.GetBytes(topic))
    {
        Topic = topic;
    }

    public SubscribeMessage(GossipMessage message) : base(MessageTypeEnum.Subscribe, message.Body)
    {
        Topic = Encoding.UTF8.GetString(message.Body);
    }
}
