using InfiniteTool.GameInterop.Internal;
using Superintendent.Core.Remote;
using System;
using System.Runtime.InteropServices;
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
			var loc = allocator.WriteString(value);
			list.AddValue(loc);
		}

		public static void AddAsciiStrings(this BlamEngineList<nint> list, ArenaAllocator allocator, string[] values)
        {
			var locations = allocator.WriteStrings(values);
			list.AddValues(locations);
        }
    }

    public unsafe class BlamEngineList<T> where T : unmanaged
	{
		IRemoteProcess proc;
		nint location;
		nint head;
		nint tail;
		nint end;
		nint count;

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

		public T[] GetValues(int index, int count)
        {
			var result = new T[count];

			if (typeof(T) == typeof(bit))
			{
				var bytes = new byte[(int)Math.Ceiling(count/8f)];
				proc.ReadSpanAt<byte>(this.head + index, bytes);

				for(var i = 0; i < count; i++)
                {
					var byteIndex = Math.DivRem(i, 8, out var bitIndex);
					result[i] = (T)(object)new bit(bytes[byteIndex], bitIndex);
				}

				return result;
			}

			proc.ReadSpanAt<byte>(this.head + sizeof(T) * index, MemoryMarshal.AsBytes<T>(result));
			return result;
		}

		public void AddValue(T value)
		{
			this.count++;
			// TODO: bit handling
			proc.WriteSpanAt<byte>(this.tail, new Span<byte>(&value, sizeof(T)));
			this.tail += sizeof(T);
			this.SyncTo();
		}

		/// <summary>
		/// Add the values to the list. !! When adding bits, it will add at the next byte, not the next bit !!
		/// </summary>
		/// <param name="values"></param>
		public void AddValues(Span<T> values)
        {
			Span<byte> bytes;
			if(typeof(T) == typeof(bit))
            {
				// TODO: implement proper bit-level appending?

				bytes = new byte[(int)Math.Ceiling(values.Length / 8f)];

				for (var i = 0; i < values.Length; i++)
				{
					var bit = (bit)(object)values[i];

					var byteIndex = Math.DivRem(i, 8, out var bitIndex);
					bytes[byteIndex] = bit.Set(bytes[byteIndex], bitIndex);					
				}
            }
			else
            {
				bytes = MemoryMarshal.AsBytes(values);
			}

			this.count += values.Length;
			proc.WriteSpanAt<byte>(this.tail, bytes);
			this.tail += bytes.Length;
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
			proc.ReadAt(this.location, out this.head);

			proc.ReadAt(this.location + sizeof(nint), out this.tail);

			proc.ReadAt(this.location + sizeof(nint) + sizeof(nint), out this.end);

			proc.ReadAt(this.location + sizeof(nint) + sizeof(nint) + sizeof(nint), out this.count);
		}

		public void SyncTo()
		{
			proc.WriteAt(this.location, this.head);

			proc.WriteAt(this.location + sizeof(nint), this.tail);

			proc.WriteAt(this.location + sizeof(nint) + sizeof(nint), this.end);

            proc.WriteAt(this.location + sizeof(nint) + sizeof(nint) + sizeof(nint), this.count);
        }

		public static implicit operator nint(BlamEngineList<T> list) => list.Address;
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

		public byte Set(byte value, int index)
        {
			if (!this.Value) return value;

			value |= (byte)(0x1 << index);

			return value;
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
