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
    private readonly ConcurrentDictionary<Multiaddress, ConcurrentQueue<WrappedMessage>> _messageQueueDict;

    public string Id => "/chat/1.0.0";

    public PubsubImitateProtocol(ILoggerFactory loggerFactory, IMessageHandler messageHandler)
    {
        _logger = loggerFactory.CreateLogger<PubsubImitateProtocol>();
        _messageHandler = messageHandler;
        _messageQueueDict = new ConcurrentDictionary<Multiaddress, ConcurrentQueue<WrappedMessage>>();
        _messageHandler.OnMessagePublished += (_, message) =>
        {
            _logger.LogDebug("Enqueue message {Message}", message);
            foreach (var queue in _messageQueueDict.Values)
            {
                queue.Enqueue(message);
            }
        };
    }

    protected override async Task ConnectAsync(IChannel channel, IChannelFactory? channelFactory, IPeerContext context, bool isListener)
    {
        _logger.LogTrace("ConnectAsync invoked: {IsListener}", isListener);
        var messageQueue = new ConcurrentQueue<WrappedMessage>();
        if (!_messageQueueDict.TryAdd(context.RemotePeer.Address, messageQueue))
        {
            _logger.LogError("Failed to connect with peer {Context}", context);
            return;
        }

        if (!isListener && _messageHandler.ListenerAddress is not null)
            messageQueue.Enqueue(new WrappedMessage(_messageHandler.ListenerAddress, new HelloMessage()));

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var ba = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                _messageHandler.ReceiveMessage((context, new WrappedMessage(ba.ToArray())));
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
