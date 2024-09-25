// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using Multiformats.Address;
using Nethermind.Libp2p.Core;
using PubsubImitateChat.Message;

namespace PubsubImitateChat;

public interface IMessageHandler
{
    void PublishMessage(GossipMessage message);

    void ReceiveMessage((IPeerContext context, WrappedMessage message) arg);

    EventHandler<(Multiaddress, Multiaddress, GossipMessage)>? OnMessageReceived { get; set; }

    Multiaddress? ListenerAddress { get; set; }

    EventHandler<WrappedMessage>? OnMessagePublished { get; set; }
}
