// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Text;

namespace PubsubImitateChat.Message;

public class DataMessage : GossipMessage
{
    public string Content;
    public DataMessage(string content) : base(MessageTypeEnum.Data, Encoding.UTF8.GetBytes(content))
    {
        Content = content;
    }

    public DataMessage(GossipMessage message) : base(MessageTypeEnum.Data, message.Body)
    {
        Content = Encoding.UTF8.GetString(message.Body);
    }
}
