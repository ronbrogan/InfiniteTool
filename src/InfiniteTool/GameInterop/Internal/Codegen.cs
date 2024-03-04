using Iced.Intel;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.Diagnostics;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace InfiniteTool.GameInterop.Internal
{
    public interface IGeneratedUtilities
    {
        nint ShowMessage(uint playerId, nint stringAddress, float duration = 5f);
    }

    public class Codegen : Assembler, IGeneratedUtilities
    {
        private nint baseAddress;
        private nint nextLocation;
        private MemoryStream asm;
        private readonly InfiniteOffsets offsets;
        private readonly IRemoteProcess proc;

        private Codegen(InfiniteOffsets offsets, IRemoteProcess proc) : base(64)
        {
            this.asm = new MemoryStream();
            this.offsets = offsets;
            this.proc = proc;
        }

        public static IGeneratedUtilities Generate(InfiniteOffsets offsets, IRemoteProcess proc)
        {
            var cg = new Codegen(offsets, proc);
            cg.GenerateShowMessage();
            cg.WriteInstructions();
            return cg;
        }

        private void WriteInstructions()
        {
            var required = ((int)Math.Ceiling(asm.Length / 4096d)) * 4096;

            var buf = this.proc.Allocate(required);
            this.baseAddress = buf;

            this.proc.WriteSpanAt<byte>(this.baseAddress, asm.ToArray());

            this.proc.SetProtection(this.baseAddress, required, MemoryProtection.ExecuteRead);
        }

        private nint showMessageOffset = -1;

        public nint ShowMessage(uint playerId, nint stringAddress, float duration = 5f)
        {
            Debug.Assert(baseAddress != 0);
            Debug.Assert(showMessageOffset >= 0);

            var durationBits = (nint)BitConverter.SingleToUInt32Bits(duration);
            return this.proc.CallFunctionAt<nint>(baseAddress + showMessageOffset, (nint)playerId, stringAddress, durationBits).Item2;
        }

        private void GenerateShowMessage()
        {
            nint getMessageBuffer = this.offsets.GetMessageBuffer;
            nint getMessageBufferSlot = this.offsets.GetMessageBufferSlot;
            nint showMessage = this.offsets.ShowMessage;

            push(r12);
            push(r13);
            push(r14);
            push(rsi);
            push(rdi);

            // our args, player ID and string pointer
            mov(r12, rcx);
            mov(r13, rdx);

            mov(rsi, this.proc.GetBaseOffset());

            mov(rdi, getMessageBuffer);
            add(rdi, rsi);
            mov(rcx, 0);
            call(rdi);
            mov(r14, rax);

            mov(rdi, getMessageBufferSlot);
            add(rdi, rsi);
            mov(rcx, r14);
            add(rcx, 0x888);
            mov(rdx, r12);
            mov(r8, r12);
            call(rdi);


            mov(rdi, showMessage);
            add(rdi, rsi);
            mov(rcx, r14);
            mov(rdx, rax);
            // xmm2 from our args is re-used here, otherwise we'd have to specify
            mov(r9, r13);
            call(rdi);

            pop(rdi);
            pop(rsi);
            pop(r14);
            pop(r13);
            pop(r12);

            ret();

            CommitAndReset(ref showMessageOffset);
        }

        private void CommitAndReset(ref nint address)
        {
            Debug.Assert(asm.Position == this.nextLocation);

            address = this.nextLocation;

            var writer = new StreamCodeWriter(asm);
            var result = this.Assemble(writer, (ulong)this.nextLocation);

            while(asm.Position % 16 != 0)
            {
                asm.WriteByte(0xcc);
            }

            this.nextLocation = (nint)asm.Position;
            this.Reset();
        }
    }
}
