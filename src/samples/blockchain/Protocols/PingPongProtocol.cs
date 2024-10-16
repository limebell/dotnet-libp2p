using System.Buffers;
using Microsoft.Extensions.Logging;
using Nethermind.Libp2p.Core;

namespace Blockchain.Protocols
{
    internal class PingPongProtocol(ConsoleInterface consoleInterface) : IProtocol
    {
        public string Id => "/blockchain/ping-pong/1.0.0";
        public async Task DialAsync(IChannel downChannel, IChannelFactory? upChannelFactory, IPeerContext context)
        {
            TimeSpan elapsed = TimeSpan.Zero;

            // Keep connection for 5 seconds
            while (elapsed < TimeSpan.FromSeconds(5))
            {
                if (consoleInterface.TryGetMessageToSend(
                        context.RemotePeer.Address,
                        out byte[]? msg))
                {
                    if (msg is { } msgNotNull)
                    {
                        await downChannel.WriteAsync(new ReadOnlySequence<byte>(msgNotNull));
                        ReadOnlySequence<byte> read = await downChannel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                        consoleInterface.ReceivePingPongMessage(read.ToArray(), _ => { });
                    }
                    elapsed = TimeSpan.Zero;
                }
                else
                {
                    await Task.Delay(50);
                    elapsed += TimeSpan.FromMilliseconds(50);
                }
            }
        }

        public async Task ListenAsync(IChannel downChannel, IChannelFactory? upChannelFactory, IPeerContext context)
        {
            while (true)
            {
                ReadOnlySequence<byte> read = await downChannel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                consoleInterface.ReceivePingPongMessage(
                    read.ToArray(),
                    msg => _ = downChannel.WriteAsync(new ReadOnlySequence<byte>(msg)));
            }
        }
    }
}
