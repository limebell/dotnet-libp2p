// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multiformats.Address;
using Multiformats.Address.Protocols;
using Nethermind.Libp2p.Core;
using PubsubImitateChat.Message;

namespace PubsubImitateChat;

public class Gossip
{
    // Remote / Listener
    public readonly ConcurrentBag<Multiaddress> Peers = new();

    private readonly IMessageHandler _messageHandler;
    private readonly IPeerFactory _peerFactory;
    private readonly ILogger _logger;

    private CancellationTokenSource _cts;

    public EventHandler<(Multiaddress, DataMessage)>? OnMessageReceived;

    public Gossip(IServiceProvider serviceProvider)
    {
        _messageHandler = serviceProvider.GetService<IMessageHandler>()!;
        _peerFactory = serviceProvider.GetService<IPeerFactory>()!;
        _messageHandler.OnMessageReceived += ReceiveMessage;
        _logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("Gossip");
        _cts = new CancellationTokenSource();
    }

    public async Task StartAsync(bool quic, string? port, CancellationToken ct)
    {
        Identity optionalFixedIdentity = new(Enumerable.Repeat((byte)42, 32).ToArray());
        ILocalPeer peer = _peerFactory.Create(optionalFixedIdentity);

        string addrTemplate = quic ?
            "/ip4/127.0.0.1/udp/{0}/quic-v1" :
            "/ip4/127.0.0.1/tcp/{0}";

        IListener listener = await peer.ListenAsync(string.Format(addrTemplate, port ?? "0"), ct);
        _logger.LogInformation("Listener started at {Address}", listener.Address);
        _messageHandler.ListenerAddress = listener.Address;
        listener.OnConnection += async remotePeer => _logger.LogInformation("A peer connected {Remote}", remotePeer.Address);
        Console.CancelKeyPress += delegate { listener.DisconnectAsync(); };

        await listener;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _cts.CancelAsync();
    }

    public async Task ConnectAsync(Multiaddress remoteAddr, CancellationToken ct)
    {
        if (remoteAddr is null) return;

        if (Peers.Contains(remoteAddr))
        {
            _logger.LogInformation("Peer table already contains peer of address {Address}, ignore", remoteAddr);
            return;
        }

        string addrTemplate = remoteAddr.Has<QUICv1>() ?
            "/ip4/127.0.0.1/udp/0/quic-v1" :
            "/ip4/127.0.0.1/tcp/0";

        ILocalPeer localPeer = _peerFactory.Create(localAddr: addrTemplate);

        _logger.LogInformation("Dialing {Remote}", remoteAddr);
        IRemotePeer remotePeer = await localPeer.DialAsync(remoteAddr, ct);

        Peers.Add(remoteAddr);
        _logger.LogInformation("Successfully add peer of address {Address} to the peer table, dialing...", remoteAddr);

        await remotePeer.DialAsync<PubsubImitateProtocol>(ct);
        await remotePeer.DisconnectAsync();
        _logger.LogInformation("Connection with peer {Address} disconnected", remoteAddr);
    }

    public void Publish(string message)
    {
        _logger.LogTrace("Publish message {Message}", message);
        _messageHandler.PublishMessage(new DataMessage(message));
    }

    public void ReceiveMessage(object? sender, (Multiaddress remote, Multiaddress listener, GossipMessage gossipMessage) arg)
    {
        _logger.LogTrace(
            "Received message {Message} from {Remote} {Listener}",
            arg.gossipMessage,
            arg.remote,
            arg.listener);
        switch (arg.gossipMessage.MessageType)
        {
            case GossipMessage.MessageTypeEnum.Hello:
                // Send back peers message
                _logger.LogInformation("New connection detected. Sending {Count} peers information: {@Peers}", Peers.Count, Peers);
                _messageHandler.PublishMessage(new PeersMessage(Peers.ToArray()));
                if (!Peers.Contains(arg.listener))
                {
                    _logger.LogInformation("Adding peer {Address} to the table", sender);
                    Peers.Add(arg.listener);
                }

                break;
            case GossipMessage.MessageTypeEnum.Peers:
                var peersMessage = new PeersMessage(arg.gossipMessage);
                _logger.LogInformation("Received PeersMessage {@Message}", peersMessage.Peers);
                foreach (var peer in peersMessage.Peers)
                {
                    if (!Peers.Contains(peer) &&
                        _messageHandler.ListenerAddress is { } addressNotNull &&
                        !addressNotNull.Equals(peer))
                    {
                        _ = ConnectAsync(peer, _cts.Token);
                    }
                }
                break;
            case GossipMessage.MessageTypeEnum.Data:
                OnMessageReceived?.Invoke(this, (arg.remote, new DataMessage(arg.gossipMessage)));
                break;
            default:
                _logger.LogError("Received unexpected message {Message}", arg.gossipMessage);
                break;
        }
    }
}
