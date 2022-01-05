﻿using Superintendent.Core.Remote;
using System;
using System.Buffers;
using System.Runtime.Serialization;

namespace InfiniteTool.GameInterop
{
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
            this.localCopy = ArrayPool<byte>.Shared.Rent(size);

            // ArrayPool will give us at least the bytes we ask for, tending towards 2^N, so we'll just use that
            this.size = this.localCopy.Length;
            this.allocationBase = remoteProcess.Allocate(size);
            this.freeSpot = this.allocationBase;
        }

        public nint Allocate(int bytes)
        {
            if(this.freeSpot + bytes > this.allocationBase + this.size)
            {
                throw new NoRoomException();
            }

            var spot = this.freeSpot;
            this.freeSpot += bytes;
            return spot;
        }

        public void Reclaim(bool zero = false)
        {
            if(zero)
            {
                // TODO: if we start writing to local copy, we'll need to zero it first
                this.RemoteProcess.WriteAt(this.allocationBase, this.localCopy) ;
            }

            this.freeSpot = this.allocationBase;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ArrayPool<byte>.Shared.Return(this.localCopy);
                }

                if(this.allocationBase > 0)
                {
                    try
                    {
                        this.RemoteProcess.Free(this.allocationBase);
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