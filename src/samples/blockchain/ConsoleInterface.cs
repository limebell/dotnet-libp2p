#nullable enable
using System.Collections.Concurrent;
using System.Text;
using Blockchain.Protocols;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain
{
    public class ConsoleInterface
    {
        private readonly ConsoleReader _consoleReader = new();
        private readonly ConcurrentDictionary<Multiaddress, int> _peerStatus = new();
        private readonly ConcurrentDictionary<Multiaddress, ConcurrentBag<byte[]>> _messagesToSend = new();
        private ILocalPeer? _localPeer;

        public event EventHandler<byte[]>? MessageToBroadcast;

        private Chain _chain;
        private MemPool _memPool;

        public ConsoleInterface(
            Chain chain,
            MemPool mempool)
        {
            _chain = chain;
            _memPool = mempool;
        }

        public async Task StartAsync(
            ILocalPeer localPeer,
            CancellationToken cancellationToken = default)
        {
            _localPeer = localPeer;
            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Write("> ");
                var input = await _consoleReader.ReadLineAsync(cancellationToken);
                if (input == "block")
                {
                    Block block = _chain.Mine(_memPool.Dump());
                    Console.WriteLine($"Created block: {block}");
                    _chain.Append(block);
                    byte[] bytes = Codec.Encode(block);
                    MessageToBroadcast?.Invoke(this, bytes);
                }
                else if (input == "tx")
                {
                    Transaction transaction = new Transaction(Guid.NewGuid().ToString());
                    Console.WriteLine($"Created transaction: {transaction}");
                    _memPool.Add(transaction);
                    byte[] bytes = Codec.Encode(transaction);
                    MessageToBroadcast?.Invoke(this, bytes);
                }
                else if (input == "sync")
                {
                    Console.WriteLine("Start blocksync");
                    // NOTE: This should normally be initiated with polling with
                    // some way of retrieving a target remote to sync.
                    (Multiaddress?, int) longest = (null, -1);
                    foreach (var peer in _peerStatus)
                    {
                        if (peer.Value > longest.Item2)
                        {
                            longest = (peer.Key, peer.Value);
                        }
                    }

                    if (longest.Item1 is { } target)
                    {
                        Console.WriteLine("Request block to {0}", target);
                        SendMessageAsync(target, [(byte)MessageType.GetBlocks]);
                    }
                    else
                    {
                        Console.WriteLine("No any target to sync blocks");
                    }
                }
                else if (input == "exit")
                {
                    Console.WriteLine("Terminating process.");
                    return;
                }
                else
                {
                    Console.WriteLine($"Unknown command: {input}");
                }
            }
        }

        public async Task ReceiveBroadcastMessage(Multiaddress sender, byte[] bytes)
        {
            byte messageType = bytes[0];
            if (messageType == (byte)MessageType.Block)
            {
                var block = new Block(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received block: {block}");
                if (_peerStatus.TryGetValue(sender, out int value))
                {
                    if (value < block.Index)
                    {
                        _peerStatus[sender] = block.Index;
                    }
                }
                else
                {
                    _peerStatus.TryAdd(sender, block.Index);
                }

                if (block.Index == _chain.Blocks.Count)
                {
                    _chain.Append(block);
                    _memPool.Remove(block.Transactions.Select(tx => tx.Id));
                    Console.WriteLine($"Appended block to current chain.");
                }
                else
                {
                    Console.WriteLine($"Ignoring block as the index does not match {_chain.Blocks.Count}.");
                }
            }
            else if (messageType == (byte)MessageType.Transaction)
            {
                var transaction = new Transaction(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received transaction: {transaction}");
                _memPool.Add(transaction);
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public void ReceivePingPongMessage(byte[] bytes, Action<byte[]> replyFunc)
        {
            byte messageType = bytes[0];
            if (messageType == (byte)MessageType.GetBlocks)
            {
                Console.WriteLine($"Received get blocks.");
                var chain =  (Codec.Encode(_chain));
                replyFunc(chain);
            }
            else if (messageType == (byte)MessageType.Blocks)
            {
                var chain = new Chain(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received {chain.Blocks.Count} blocks.");
                var appended = 0;
                foreach (var block in chain.Blocks)
                {
                    if (block.Index >= _chain.Blocks.Count)
                    {
                        _chain.Append(block);
                        appended++;
                    }
                }

                Console.WriteLine($"Appended {appended} blocks to current chain.");
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public bool TryGetMessageToSend(Multiaddress target, out byte[]? msg)
        {
            msg = null;
            return _messagesToSend.TryGetValue(target, out var value) && value.TryTake(out msg);
        }

        private async void SendMessageAsync(Multiaddress target, byte[] msg)
        {
            if (_localPeer is not { } localPeer) return;
            try
            {
                Console.WriteLine("Dialing {0}", target);
                IRemotePeer remotePeer = await localPeer.DialAsync(target);
                Console.WriteLine("Dialing {0} complete", target);
                _ = remotePeer.DialAsync<PingPongProtocol>()
                    .ContinueWith(_ => remotePeer.DisconnectAsync());
            }
            catch (Exception e)
            {
                Console.WriteLine("Error occurred during SendMessageAsync: {0}", e);
                // Continue only when duplicated connection?
            }

            if (_messagesToSend.TryGetValue(target, out var value))
            {
                value.Add(msg);
            }
            else
            {
                _messagesToSend.TryAdd(target, new ConcurrentBag<byte[]>([msg]));
            }
        }
    }
}
