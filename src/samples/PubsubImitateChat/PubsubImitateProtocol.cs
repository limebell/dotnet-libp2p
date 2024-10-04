// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Multiformats.Address;
using Nethermind.Libp2p.Core;
using PubsubImitateChat.Message;

namespace PubsubImitateChat;

public class PubsubImitateProtocol : SymmetricProtocol, IProtocol
{
    private readonly ILogger _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly ConcurrentDictionary<string, ConcurrentBag<Multiaddress>> _topics;
    private readonly ConcurrentDictionary<Multiaddress, ConcurrentQueue<WrappedMessage>> _messageQueueDict;

    public string Id => "/chat/1.0.0";

    public PubsubImitateProtocol(ILoggerFactory loggerFactory, IMessageHandler messageHandler)
    {
        _logger = loggerFactory.CreateLogger<PubsubImitateProtocol>();
        _messageHandler = messageHandler;
        _topics = new ConcurrentDictionary<string, ConcurrentBag<Multiaddress>>();
        _messageQueueDict = new ConcurrentDictionary<Multiaddress, ConcurrentQueue<WrappedMessage>>();
        _messageHandler.OnMessagePublished += (_, message) =>
        {
            _logger.LogDebug("Enqueue message {Message}", message);
            foreach (var kv in _messageQueueDict)
            {
                if (message.Body.MessageType == GossipMessage.MessageTypeEnum.Data)
                {
                    var dataMessage = new DataMessage(message.Body);
                    _logger.LogTrace(
                        "Trying to enqueue data message {Message} with topic {Topic}",
                        dataMessage,
                        dataMessage.Topic);
                    if (!_topics.TryGetValue(dataMessage.Topic, out ConcurrentBag<Multiaddress>? bag) ||
                        !bag.Contains(kv.Key))
                    {
                        continue;
                    }
                }

                kv.Value.Enqueue(message);
            }
        };
    }

    protected override async Task ConnectAsync(IChannel channel, IChannelFactory? channelFactory, IPeerContext context, bool isListener)
    {
        _logger.LogTrace("ConnectAsync invoked: {IsListener}", isListener);
        var messageQueue = new ConcurrentQueue<WrappedMessage>();
        if (!isListener)
        {
            if (!_messageQueueDict.TryAdd(context.RemotePeer.Address, messageQueue))
            {
                _logger.LogError("Failed to connect with peer {Context}", context);
                return;
            }

            if (_messageHandler.ListenerAddress is not null)
                messageQueue.Enqueue(new WrappedMessage(_messageHandler.ListenerAddress, new HelloMessage()));
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var ba = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                var wrappedMessage = new WrappedMessage(ba.ToArray());
                if (wrappedMessage.Body.MessageType == GossipMessage.MessageTypeEnum.Hello)
                {
                    _messageQueueDict.TryAdd(wrappedMessage.ListenerAddress, messageQueue);
                    _messageHandler.ReceiveMessage((context, wrappedMessage));
                }
                else if (wrappedMessage.Body.MessageType == GossipMessage.MessageTypeEnum.Subscribe)
                {
                    var topic = new SubscribeMessage(wrappedMessage.Body).Topic;
                    _logger.LogTrace(
                        "Peer {Address} subscribed to topic {Topic}",
                        wrappedMessage.ListenerAddress,
                        topic);
                    if (_topics.TryGetValue(topic, out ConcurrentBag<Multiaddress>? bag))
                    {
                        if (!bag.Contains(wrappedMessage.ListenerAddress))
                        {
                            bag.Add(wrappedMessage.ListenerAddress);
                        }
                    }
                    else
                    {
                        var concurrentBag = new ConcurrentBag<Multiaddress>();
                        concurrentBag.Add(wrappedMessage.ListenerAddress);
                        _topics.TryAdd(topic, concurrentBag);
                        _logger.LogTrace(
                            "Successfully added peer {Address} to topic {Topic}",
                            wrappedMessage.ListenerAddress,
                            topic);
                    }
                }
                else
                {
                    _messageHandler.ReceiveMessage((context, wrappedMessage));
                }
            }
        });

        while (true)
        {
            _logger.LogTrace("Checking MessageQueue...");
            while (messageQueue.Any())
            {
                if (messageQueue.TryDequeue(out var message))
                {
                    _logger.LogTrace("Sending message {Message} to {Address}", message.Body, context.RemotePeer.Address);
                    await channel.WriteAsync(new ReadOnlySequence<byte>(message.Serialize()));
                }
            }

            await Task.Delay(50);
        }
    }
}
