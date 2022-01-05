using Superintendent.Core.Remote;
using System;
using System.Text;

namespace InfiniteTool.GameInterop.EngineDataTypes
{
	public static class BlamEngineListExtensions
    {
		public static BlamEngineList<T> AllocateList<T>(this ArenaAllocator allocator, int listSize) where T: unmanaged
        {
			var (headSize, bodySize) = BlamEngineList<T>.GetRequiredSize(listSize);
			var location = allocator.Allocate(headSize + bodySize);
			return new BlamEngineList<T>(allocator.RemoteProcess, location, location + headSize, listSize);
        }

		public static void AddAsciiString(this BlamEngineList<nint> list, ArenaAllocator allocator, string value)
        {
			Span<byte> keyBytes = stackalloc byte[Encoding.ASCII.GetByteCount(value) + 1];
			Encoding.ASCII.GetBytes(value, keyBytes);
			var loc = allocator.Allocate(keyBytes.Length);
			allocator.RemoteProcess.WriteAt(loc, keyBytes);
			list.AddValue(loc);
		}
    }

    public unsafe class BlamEngineList<T> where T : unmanaged
	{
		IRemoteProcess proc;
		nint location;
		nint head;
		nint tail;
		nint end;

		public nint Address => location;

		public BlamEngineList(IRemoteProcess proc, nint address, nint storageAddress, int capacity)
		{
			this.proc = proc;
			this.location = address;
			this.head = storageAddress;
			this.tail = storageAddress;

			if (typeof(T) == typeof(bit))
			{
				var bytesRequired = ((capacity + 7) / 8);
				this.end = storageAddress + bytesRequired;
			}
			else
			{
				this.end = storageAddress + capacity * sizeof(T);
			}

			this.SyncTo();
		}

		private BlamEngineList(IRemoteProcess proc, nint address)
		{
			this.proc = proc;
			this.location = address;
		}

		public static BlamEngineList<T> FromExisting<T>(IRemoteProcess proc, nint address) where T : unmanaged
		{
			var list = new BlamEngineList<T>(proc, address);
			list.SyncFrom();

			return list;
		}

		public static (int header, int body) GetRequiredSize(int capacity)
		{
			return (sizeof(nint) * 4, sizeof(T) * capacity);
		}

		public T GetValue(int index)
		{
			if (typeof(T) == typeof(bit))
			{
				var byteIndex = Math.DivRem(index, 8, out var bitIndex);
				proc.ReadAt(this.head + byteIndex, out byte byteVal);
				return (T)(object)new bit(byteVal, bitIndex);
			}
			else
			{
				proc.ReadAt(this.head + sizeof(T) * index, out T value);
				return value;
			}
		}

		public void AddValue(T value)
		{
			// TODO: bit handling
			proc.WriteAt(this.tail, new Span<byte>(&value, sizeof(T)));
			this.tail += sizeof(T);
			this.SyncTo();
		}

		public void SetValue(int index, T value)
		{
			// TODO: bit handling
			proc.WriteAt(this.head + sizeof(T) * index, value);
		}

		public nint Count()
		{
			return (this.tail - this.head) / sizeof(T);
		}

		public void SyncFrom()
		{
			nint val = 0;
			var bytes = new Span<byte>(&val, sizeof(nint));

			proc.ReadAt(this.location, bytes);
			this.head = val;

			proc.ReadAt(this.location + sizeof(nint), bytes);
			this.tail = val;

			proc.ReadAt(this.location + sizeof(nint) + sizeof(nint), bytes);
			this.end = val;
		}

		public void SyncTo()
		{
			nint val = 0;
			var bytes = new Span<byte>(&val, sizeof(nint));

			val = this.head;
			proc.WriteAt(this.location, bytes);

			val = this.tail;
			proc.WriteAt(this.location + sizeof(nint), bytes);

			val = this.end;
			proc.WriteAt(this.location + sizeof(nint) + sizeof(nint), bytes);
		}
	}

	public struct bit
	{
		public bool Value;

		public bit(bool value)
		{
			this.Value = value;
		}

		public bit(byte value, int index)
		{
			this.Value = ((value >> index) & 0x1) == 0x1;
		}

		public static implicit operator bit(bool value) => new bit(value);
		public static implicit operator bool(bit value) => value.Value;

		public static explicit operator uint(bit value) => (uint)(value.Value ? 1 : 0);

		public override string ToString()
		{
			return Value.ToString();
		}
	}
}
