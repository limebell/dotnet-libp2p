// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using Multiformats.Address;
using Nethermind.Libp2p.Core;
using PubsubImitateChat.Message;

namespace PubsubImitateChat;

public class MessageHandler : IMessageHandler
{
    public void PublishMessage(GossipMessage message)
    {
        if (ListenerAddress is null)
        {
            throw new ArgumentNullException(nameof(ListenerAddress));
        }

        var wrapped = new WrappedMessage(ListenerAddress, message);
        OnMessagePublished?.Invoke(this, wrapped);
    }

    public void ReceiveMessage((IPeerContext context, WrappedMessage message) arg)
    {
        var gossipMessage = arg.message.Body;
        OnMessageReceived?.Invoke(this, (arg.context.RemotePeer.Address, arg.message.ListenerAddress, gossipMessage));
    }

    public EventHandler<(Multiaddress, Multiaddress, GossipMessage)>? OnMessageReceived { get; set; }

    public Multiaddress? ListenerAddress { get; set; }

    public EventHandler<WrappedMessage>? OnMessagePublished { get; set; }
}
