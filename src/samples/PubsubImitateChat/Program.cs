// See https://aka.ms/new-console-template for more information

using Nethermind.Libp2p.Stack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multiformats.Address;
using PubsubImitateChat;
using PubsubImitateChat.Message;

ServiceProvider serviceProvider = new ServiceCollection()
    .AddSingleton<IMessageHandler, MessageHandler>()
    .AddLibp2p(builder => builder.AddAppLayerProtocol<PubsubImitateProtocol>())
    .AddLogging(builder =>
        builder.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information)
            .AddSimpleConsole(l =>
            {
                l.SingleLine = true;
                l.TimestampFormat = "[HH:mm:ss.FFF]";
            }))
    .BuildServiceProvider();

ILogger logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("PubsubImitateChat");
Gossip gossip = new(serviceProvider);

CancellationTokenSource ts = new();
logger.LogInformation("Initialize Service...");
_ = gossip.StartAsync(args.Contains("-quic"), args.Length > 0 && args[0] == "-sp" ? args[1] : null, ts.Token);
gossip.OnMessageReceived += (_, pair) =>
{
    string address = pair.Item1.ToString() ?? "        ";
    Console.WriteLine("[{0}] {1}", address.Substring(address.Length - 6), pair.Item2.Content);
};

if (args.Length > 0 && args[0] == "-d")
{
    _ = gossip.ConnectAsync((Multiaddress)args[1], ts.Token);
}

while (true)
{
    var message = Console.ReadLine();
    if (message != null) gossip.Publish(message);
}

/*Multiaddress[] addresses = { "/ip4/127.0.0.1/tcp/50354/p2p/12D3KooWGv47rwW57sXkQs2Ew4Rday8byyVmUFkrGd1fCLYPRErG", "/ip4/127.0.0.1/tcp/50352/p2p/12D3KooWBXu3uGPMkjjxViK6autSnFH5QaKJgTwW8CaSxYSD6yYL" };
var peersMessage = new PeersMessage(addresses);
byte[] serialized = peersMessage.Serialize();
PeersMessage deserialized = new PeersMessage(new GossipMessage(serialized));
Console.WriteLine("Equals? {0}", peersMessage.Peers.Count == deserialized.Peers.Count);
Console.WriteLine("Peers? {0}", deserialized.Peers.Aggregate("", (s, multiaddress) => s + multiaddress + ", "));*/
