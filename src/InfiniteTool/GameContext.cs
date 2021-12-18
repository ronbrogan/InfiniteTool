using Microsoft.Extensions.Logging;
using Superintendent.CommandSink;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace InfiniteTool
{
    public class InfiniteOffsets
    {
        public nint MainThreadEntry => 0x467010;
        public nint CheckpointIndexOffset => 0x49DF8BC;
        public nint RevertFlagOffset => 0x44FFEC8;
    }

    public class GameContext : INotifyPropertyChanged
    {
        private readonly ILogger<GameContext> logger;
        private readonly InfiniteOffsets offsets;
        private (IntPtr handle, TEB teb) mainThread = (IntPtr.Zero, default);

        public RpcRemoteProcess RemoteProcess { get; private set; }
        public ICommandSink? ProcessSink { get; private set; }
        public int ProcessId { get; private set; }

        public nint CheckpointFlagAddress;
        public nint RevertFlagOffset;

        public event PropertyChangedEventHandler? PropertyChanged;

        public GameContext(ILogger<GameContext> logger)
        {
            this.logger = logger;
            this.offsets = new InfiniteOffsets();
            this.RemoteProcess = new RpcRemoteProcess();
        }

        public async Task Initialize()
        {
            logger.LogInformation("Setting up context");
            this.RemoteProcess.ProcessAttached += RemoteProcess_ProcessAttached;
            this.RemoteProcess.ProcessDetached += RemoteProcess_ProcessDetached;
            await this.RemoteProcess.Attach("HaloInfinite");
        }

        internal void TriggerRevert()
        {
            this.logger.LogInformation("Revert requested");
            this.ProcessSink?.Write(this.RevertFlagOffset, stackalloc byte[] { 0x22 });
        }

        internal void TriggerCheckpoint()
        {
            this.logger.LogInformation("Checkpoint requested");
            this.ProcessSink?.WriteAt(this.CheckpointFlagAddress, stackalloc byte[] { 0x3 });
            this.ProcessSink?.WriteAt(this.CheckpointFlagAddress+4, stackalloc byte[] { 0x0 });
            this.ProcessSink?.WriteAt(this.CheckpointFlagAddress+12, stackalloc byte[] { 0x3 });
        }

        private void RemoteProcess_ProcessAttached(object? sender, ProcessAttachArgs e)
        {
            if (ProcessId == e.ProcessId)
            {
                logger.LogWarning("Redundant process attach callback fired for pid {PID}", e.ProcessId);
                return;
            }

            this.ProcessId = e.ProcessId;
            this.ProcessSink = this.RemoteProcess.GetCommandSink("HaloInfinite.exe");
            this.Bootstrap();
        }

        private void RemoteProcess_ProcessDetached(object? sender, EventArgs e)
        {
            logger.LogInformation("Detaching from process {PID}", this.ProcessId);
            this.ProcessId = 0;
            this.ProcessSink = null;
            this.RevertFlagOffset = 0;
            this.CheckpointFlagAddress = 0;
        }

        public void Bootstrap()
        {
            logger.LogInformation("Bootstrapping for new process {PID}", this.ProcessId);
            try
            {
                GetMainThreadInfo();
                PopulateAddresses();
            }
            catch { }
        }

        public unsafe void PopulateAddresses()
        {
            this.RevertFlagOffset = this.offsets.RevertFlagOffset;

            Span<byte> v = stackalloc byte[4];
            this.ProcessSink!.Read(this.offsets.CheckpointIndexOffset, v);
            var checkpointIndex = BitConverter.ToInt32(v);
            this.logger.LogInformation("Found checkpoint TLS index: {index}", checkpointIndex);

            this.CheckpointFlagAddress = this.ReadMainTebPointer(checkpointIndex);
            this.logger.LogInformation("Found checkpoint flag: {offset}", this.CheckpointFlagAddress);
        }

        private unsafe void GetMainThreadInfo()
        {
            IntPtr handle = Win32.OpenProcess(AccessPermissions.QueryInformation | AccessPermissions.VmRead, false, this.ProcessId);
            if (handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var hinfBase = this.ProcessSink!.GetBaseOffset();
            var expectedThreadEntry = hinfBase + offsets.MainThreadEntry;

            var tinfo = new THREAD_BASIC_INFORMATION();
            TEB teb = default;

            foreach (var thread in this.RemoteProcess.Threads)
            {

                var thandle = Win32.OpenThread(ThreadAccess.QUERY_INFORMATION, false, (uint)thread.Id);

                if (thandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var entry = new IntPtr();
                var hr = Win32.NtQueryInformationThread(thandle, ThreadInformationClass.ThreadQuerySetWin32StartAddress, ref entry, Marshal.SizeOf(entry), out var entryReq);
                if (hr != 0)
                    throw new Win32Exception(hr);

                if (entry != expectedThreadEntry)
                {
                    continue;
                }


                hr = Win32.NtQueryInformationThread(thandle, ThreadInformationClass.ThreadBasicInformation, ref tinfo, Marshal.SizeOf(tinfo), out var tinfoReq);
                if (hr != 0)
                    throw new Win32Exception(hr);

                Span<byte> b = stackalloc byte[sizeof(TEB)];
                this.ProcessSink.ReadAt(tinfo.TebBaseAddress, b);
                Unsafe.Copy(ref teb, Unsafe.AsPointer(ref b[0]));

                mainThread = (thandle, teb);

                logger.LogInformation($"TID: {thread.Id,5:x}, ENTRY: {entry:x16}, TEB: {tinfo.TebBaseAddress:x16}, TLS Expansion: {teb.TlsExpansionSlots:x16}");

                Win32.CloseHandle(thandle);
            }

            var checkpoint = ReadMainTebPointer(304);

            logger.LogInformation($"Checkpoint: {checkpoint:x16}");

            Win32.CloseHandle(handle);
        }

        public nint ReadMainTebPointer(int index)
        {
            Span<byte> p = stackalloc byte[8];
            this.ProcessSink!.ReadAt((mainThread.teb.TlsExpansionSlots + 8 * index - 0x200), p);
            return (nint)BitConverter.ToInt64(p);
        }
    }
}
