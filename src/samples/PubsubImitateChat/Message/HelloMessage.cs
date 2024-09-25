// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

namespace PubsubImitateChat.Message;

public class HelloMessage : GossipMessage
{
    public HelloMessage() : base(MessageTypeEnum.Hello, Array.Empty<byte>())
    {
    }

    public HelloMessage(GossipMessage message) : base(MessageTypeEnum.Hello, message.Body)
    {
    }
}
