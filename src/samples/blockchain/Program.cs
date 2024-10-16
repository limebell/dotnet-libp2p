using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethermind.Libp2p.Stack;
using Nethermind.Libp2p.Core;
using Multiformats.Address;
using Blockchain.Protocols;
using Nethermind.Libp2p.Protocols;
using Nethermind.Libp2p.Protocols.Pubsub;

namespace Blockchain
{
    public static class Program
    {
        public const string ListenerAddressFileName = "listener.txt";

        public static async Task Main(string[] args)
        {
            var consoleInterface = new ConsoleInterface(new Chain(), new MemPool());
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddLibp2p(builder => builder
                    .AddAppLayerProtocol<HandshakeProtocol>(new HandshakeProtocol(consoleInterface))
                    .AddAppLayerProtocol<BroadcastProtocol>(new BroadcastProtocol(consoleInterface))
                    .AddAppLayerProtocol<PingPongProtocol>(new PingPongProtocol(consoleInterface)))
                .AddLogging(builder =>
                    builder.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information)
                        .AddSimpleConsole(l =>
                        {
                            l.SingleLine = true;
                            l.TimestampFormat = "[HH:mm:ss.FFF]";
                        }))
                .BuildServiceProvider();


            CancellationTokenSource ts = new();

            await RunTransport(serviceProvider, consoleInterface, args, ts.Token);
        }

        public static async Task RunTransport(
            ServiceProvider serviceProvider,
            ConsoleInterface consoleInterface,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            ILogger logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("Chat");
            IPeerFactory peerFactory = serviceProvider.GetService<IPeerFactory>()!;

            /*string addrTemplate = "/ip4/127.0.0.1/tcp/{0}";
            var addr = string.Format(
                addrTemplate,
                args.Length > 0 && args[0] == "-sp" ? args[1] : "0");*/
            Identity localPeerIdentity = new();
            string addr = $"/ip4/0.0.0.0/tcp/0/p2p/{localPeerIdentity.PeerId}";
            ILocalPeer peer = peerFactory.Create(localPeerIdentity, Multiaddress.Decode(addr));
            Task consoleTask = consoleInterface.StartAsync(peer, cancellationToken);

            PubsubRouter router = serviceProvider.GetService<PubsubRouter>()!;
            ITopic topic = router.GetTopic("blockchain:broadcast");
            topic.OnMessage += bytes =>
            {
                int addressLength = BitConverter.ToInt32(bytes[..4]);
                Multiaddress sender = Multiaddress.Decode(
                    Encoding.UTF8.GetString(bytes[4..(4 + addressLength)]));
                byte[] msg = bytes[(4 + addressLength)..];
                logger.LogTrace("Received message {Message}", msg.Aggregate("", (s, b) => s + b));
                _ = consoleInterface.ReceiveBroadcastMessage(sender, msg);
            };

            IListener listener = await peer.ListenAsync(addr, cancellationToken);
            Multiaddress listenerAddress = listener.Address;

            _ = serviceProvider.GetService<MDnsDiscoveryProtocol>()!.DiscoverAsync(peer.Address, token: cancellationToken);
            _ = router.RunAsync(peer, token: cancellationToken);

            consoleInterface.MessageToBroadcast += (_, msg) =>
            {
                logger.LogTrace("Publish Message: {Message}", msg.Aggregate("", (s, b) => s + b));
                // NOTE: Use ToString instead of ToBytes because it has bug
                byte[] listenerAddressBytes = Encoding.UTF8.GetBytes(listenerAddress.ToString());
                topic.Publish(BitConverter.GetBytes(listenerAddressBytes.Length)
                    .Concat(listenerAddressBytes)
                    .Concat(msg).ToArray());
            };

            using (StreamWriter outputFile = new StreamWriter(ListenerAddressFileName, false))
            {
                outputFile.WriteLine(listenerAddress.ToString());
            }
            logger.LogInformation("Listener started at {address}", listenerAddress);

            listener.OnConnection += remotePeer =>
            {
                logger.LogInformation("A peer connected {remote}", remotePeer.Address);
                return Task.CompletedTask;
            };
            Console.CancelKeyPress += delegate { listener.DisconnectAsync(); };

            await listener;
        }

        public static async Task RunClient(
            ILogger logger,
            IPeerFactory peerFactory,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Running as a client");

            string addrTemplate = "/ip4/127.0.0.1/tcp/0";
            ILocalPeer localPeer = peerFactory.Create(localAddr: addrTemplate);

            Multiaddress remoteAddr;
            using (StreamReader inputFile = new StreamReader(ListenerAddressFileName))
            {
                remoteAddr = inputFile.ReadLine();
            }
            logger.LogTrace("Dialing {remote}", remoteAddr);
            IRemotePeer remotePeer = await localPeer.DialAsync(remoteAddr, cancellationToken);

            await remotePeer.DialAsync<HandshakeProtocol>(cancellationToken);
            await remotePeer.DisconnectAsync();
        }
    }
}
