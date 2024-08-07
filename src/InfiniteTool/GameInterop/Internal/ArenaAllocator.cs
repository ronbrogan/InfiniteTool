﻿using Superintendent.Core.Remote;
using System;
using System.Buffers;
using System.Runtime.Serialization;
using System.Text;

namespace InfiniteTool.GameInterop.Internal
{
    public static class AllocatorExtensions
    {
        public static nint WriteString(this ArenaAllocator allocator, string value, Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;

            Span<byte> stringBytes = stackalloc byte[encoding.GetByteCount(value) + 4];
            encoding.GetBytes(value, stringBytes);
            var loc = allocator.Allocate(stringBytes.Length);
            allocator.RemoteProcess.WriteSpanAt<byte>(loc, stringBytes);
            return loc;
        }

        public static nint[] WriteStrings(this ArenaAllocator allocator, string[] values, Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;

            var bufSize = 0;

            foreach (var value in values)
            {
                bufSize += encoding.GetByteCount(value) + 1;
            }

            var loc = allocator.Allocate(bufSize);

            var current = 0;
            var buf = new byte[bufSize].AsSpan();

            var i = 0;
            var locations = new nint[values.Length];

            foreach (var value in values)
            {
                locations[i] = loc + current;
                current += encoding.GetBytes(value, buf.Slice(current));
                current++;
                i++;
            }

            allocator.RemoteProcess.WriteSpanAt<byte>(loc, buf);
            return locations;
        }
    }

    public class ArenaAllocator : IDisposable
    {
        public IRemoteProcess RemoteProcess { get; private set; }

        // Currently not used except for zeroing
        private byte[] localCopy;

        private int size;
        private nint allocationBase;
        private nint freeSpot;
        private bool disposedValue;

        public ArenaAllocator(IRemoteProcess remoteProcess, int size)
        {
            this.RemoteProcess = remoteProcess;
            localCopy = ArrayPool<byte>.Shared.Rent(size);

            // ArrayPool will give us at least the bytes we ask for, tending towards 2^N, so we'll just use that
            this.size = localCopy.Length;
            allocationBase = remoteProcess.Allocate(this.size);
            freeSpot = allocationBase;
        }

        public nint Allocate(int bytes)
        {
            if (freeSpot + bytes > allocationBase + size)
            {
                throw new NoRoomException();
            }

            var spot = freeSpot;
            freeSpot += bytes;
            return spot;
        }

        public void Reclaim(bool zero = false)
        {
            if (zero)
            {
                // TODO: if we start writing to local copy, we'll need to zero it first
                this.RemoteProcess.WriteSpanAt<byte>(allocationBase, localCopy);
            }

            freeSpot = allocationBase;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ArrayPool<byte>.Shared.Return(localCopy);
                }

                if (allocationBase > 0)
                {
                    try
                    {
                        this.RemoteProcess.Free(allocationBase);
                    }
                    catch { }
                }

                disposedValue = true;
            }
        }

        // 'Dispose(bool disposing)' has code to free unmanaged resources
        ~ArenaAllocator()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public class NoRoomException : Exception
        {
            public NoRoomException()
            {
            }

            public NoRoomException(string? message) : base(message)
            {
            }

            public NoRoomException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected NoRoomException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
