using System.Text;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain
{
    public class ConsoleInterface
    {
        private static ConsoleReader _consoleReader = new ConsoleReader();
        private Func<byte[], Task>? _toSendMessageTask = null;
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
            CancellationToken cancellationToken = default)
        {
            while (true)
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
                    // NOTE: This should normally be initiated with polling with
                    // some way of retrieving a target remote to sync.
                    byte[] bytes = { (byte)MessageType.GetBlocks };
                    if (_toSendMessageTask is { } toTask)
                    {
                        await toTask(bytes);
                    }
                    else
                    {
                        throw new NullReferenceException();
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

        public async Task ReceivePingPongMessage(
            byte[] bytes,
            Func<byte[], Task> toSendReplyMessageTask,
            CancellationToken cancellationToken = default)
        {
            byte messageType = bytes[0];
            if (messageType == (byte)MessageType.GetBlocks)
            {
                Console.WriteLine($"Received get blocks.");
                await toSendReplyMessageTask(Codec.Encode(_chain));
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

        public void SetToSendMessageTask(Func<byte[], Task> toSendMessageTask)
        {
            _toSendMessageTask = toSendMessageTask;
        }
    }
}
