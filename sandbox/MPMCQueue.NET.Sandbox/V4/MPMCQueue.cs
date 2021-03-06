﻿using System;
using System.Threading;
using Padded.Fody;

namespace MPMCQueue.NET.Sandbox.V4
{
    [Padded]
    public class MPMCQueue<T>
    {
        private readonly Cell[] _buffer;
        private readonly int _bufferMask;

        private int _enqueuePos;
        private int _dequeuePos;


        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException($"{nameof(bufferSize)} should be greater than 2");
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException($"{nameof(bufferSize)} should be a power of 2");

            _bufferMask = bufferSize - 1;
            _buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _buffer[i].Sequence = i;
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        public bool TryEnqueue(T item)
        {
            do
            {
                var pos = _enqueuePos;
                var index = pos & _bufferMask;
                var cell = _buffer[index];
                if (cell.Sequence - pos == 0 && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    cell.Element = item;
                    cell.Sequence = pos + 1;
                    _buffer[index] = cell;
                    return true;
                }

                if (cell.Sequence - pos < 0)
                {
                    return false;
                }
            } while (true);
        }

        public bool TryDequeue(out T result)
        {
            do
            {
                var pos = _dequeuePos;
                var index = pos & _bufferMask;
                var cell = _buffer[index];
                if (cell.Sequence - (pos + 1) == 0 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = cell.Element;
                    cell.Sequence = pos + _bufferMask + 1;
                    _buffer[index] = cell;
                    return true;
                }

                if (cell.Sequence - (pos + 1) < 0)
                {
                    result = default(T);
                    return false;
                }
            } while (true);
        }

        private struct Cell
        {
            public volatile int Sequence;
            public T Element;
        }
    }
}