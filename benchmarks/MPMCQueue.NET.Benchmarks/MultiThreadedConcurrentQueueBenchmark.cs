﻿using System.Collections.Concurrent;
using System.Threading;
using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(SingleRunConfig))]
    public class MultiThreadedConcurrentQueueBenchmark
    {
        private readonly Message _msg = new Message() { IsWorking = true };
        private readonly Message _stopMsg = new Message() { IsWorking = false };

        private const int Operations = 1 << 26;

        //[Params(1, 2, 4, 8, 16, 32)]
        [Params(1, 2, 4)]
        public int NumberOfThreads { get; set; }


        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private ConcurrentQueue<object> _queue;
        private Thread[] _threads;
        private Thread[] _threadsConsumers;

        [Setup]
        public void Setup()
        {
            _queue = new ConcurrentQueue<object>();

            for (int i = 0; i < Operations/ NumberOfThreads; i++)
            {
                _queue.Enqueue(_msg);
            }
            for (int i = 0; i < Operations/ NumberOfThreads; i++)
            {
                object ret;
                _queue.TryDequeue(out ret);
            }

            _threadsConsumers = LaunchConsumers(NumberOfThreads);
            _threads = LaunchProducers(Operations, NumberOfThreads);
        }

        private Thread[] LaunchConsumers(int numberOfThreads)
        {
            var threads = new Thread[numberOfThreads];
            for (var i = 0; i < numberOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    var isWorking = true;
                    while (isWorking)
                    {
                        object ret;
                        if (_queue.TryDequeue(out ret))
                        {
                            isWorking = ((Message)ret).IsWorking;
                        }
                    }
                });
                thread.Start();
                threads[i] = thread;
            }
            return threads;
        }

        private Thread[] LaunchProducers(int numberOfOperations, int numberOfThreads)
        {
            var threads = new Thread[numberOfThreads];
            var opsPerThread = numberOfOperations/numberOfThreads;
            for (var i = 0; i < numberOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    _reset.Wait();
                    for (var j = 0; j < opsPerThread; j++)
                    {
                        _queue.Enqueue(_msg);
                    }
                });
                thread.Start();
                threads[i] = thread;
            }
            return threads;
        }

        [Benchmark(OperationsPerInvoke = Operations)]
        public void EnqueueDequeue()
        {
            _reset.Set();
            for (var i = 0; i < _threads.Length; i++)
            {
                _threads[i].Join();
            }

            for (var i = 0; i < NumberOfThreads * 8; i++)
            {
                _queue.Enqueue(_stopMsg);
            }

            for (var i = 0; i < _threadsConsumers.Length; i++)
            {
                _threadsConsumers[i].Join();
            }

        }
    }
}