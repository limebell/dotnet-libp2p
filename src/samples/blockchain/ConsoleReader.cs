// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Text;
using Nethermind.Libp2p.Core;

namespace blockchain
{
    internal class ConsoleReader
    {
        private readonly Queue<TaskCompletionSource<string>> _requests = new();
        private bool _isRequested;

        public Task<string> ReadLineAsync(CancellationToken token = default)
        {
            TaskCompletionSource<string> result = new();
            token.Register(() => { result.SetResult(""); });
            _requests.Enqueue(result);
            if (!_isRequested)
            {
                _isRequested = true;
                Task.Run(() =>
                {
                    string input = Console.ReadLine() ??
                        throw new NullReferenceException();
                    while (_requests.TryDequeue(out TaskCompletionSource<string>? src))
                    {
                        Task.Run(() => src.SetResult(input));
                    }

                    _isRequested = false;
                });
            }

            return result.Task;
        }
    }
}
