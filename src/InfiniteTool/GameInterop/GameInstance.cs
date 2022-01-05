using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop
{
    [AddINotifyPropertyChangedInterface]
    public class GameInstance
    {
        private readonly ILogger<GameInstance> logger;
        private readonly InfiniteOffsets offsets;

        public RpcRemoteProcess RemoteProcess { get; }

        private (IntPtr handle, TEB teb) mainThread = (IntPtr.Zero, default);

        public int ProcessId { get; private set; }

        public nint CheckpointFlagAddress;
        public nint RevertFlagOffset;

        public event EventHandler<EventArgs>? OnAttach;
        public event EventHandler<EventArgs>? BeforeDetach;

        public GameInstance(InfiniteOffsets offsets, ILogger<GameInstance> logger)
        {
            this.logger = logger;
            this.offsets = offsets;
            this.RemoteProcess = new RpcRemoteProcess();
        }

        internal void TriggerRevert()
        {
            this.logger.LogInformation("Revert requested");
            this.RemoteProcess.Write(this.RevertFlagOffset, stackalloc byte[] { 0x22 });
        }

        internal void TriggerCheckpoint()
        {
            this.logger.LogInformation("Checkpoint requested");
            this.RemoteProcess.WriteAt(this.CheckpointFlagAddress, stackalloc byte[] { 0x3 });
            this.RemoteProcess.WriteAt(this.CheckpointFlagAddress + 4, stackalloc byte[] { 0x0 });
            this.RemoteProcess.WriteAt(this.CheckpointFlagAddress + 12, stackalloc byte[] { 0x3 });
        }

        public void Initialize()
        {
            logger.LogInformation("Setting up context");
            this.RemoteProcess.ProcessAttached += RemoteProcess_ProcessAttached;
            this.RemoteProcess.ProcessDetached += RemoteProcess_ProcessDetached;
            this.RemoteProcess.AttachException += RemoteProcess_AttachException;

            try
            {
                this.RemoteProcess.Attach("HaloInfinite");
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 0x5/*ACCESS_DENIED*/)
                {
                    App.RestartAsAdmin();
                }
            }
        }

        private void RemoteProcess_AttachException(object? sender, AttachExceptionArgs e)
        {
            if (e.Exception is Win32Exception w32ex && w32ex.NativeErrorCode == 0x5/*ACCESS_DENIED*/)
            {
                App.RestartAsAdmin();
            }
            else
            {
                this.logger.LogError(e.Exception, "ProcessAttach exception, pid {pid}", e.ProcessId);
            }
        }

        private void RemoteProcess_ProcessAttached(object? sender, ProcessAttachArgs e)
        {
            if (ProcessId == e.ProcessId)
            {
                logger.LogWarning("Redundant process attach callback fired for pid {PID}", e.ProcessId);
                return;
            }

            this.ProcessId = e.ProcessId;
            this.Bootstrap();
            this.OnAttach?.Invoke(this, new EventArgs());
        }

        private void RemoteProcess_ProcessDetached(object? sender, EventArgs e)
        {
            logger.LogInformation("Detaching from process {PID}", this.ProcessId);
            this.ProcessId = 0;
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

            this.RemoteProcess.Read(this.offsets.Checkpoint_TlsIndexOffset, out int checkpointIndex);
            this.logger.LogInformation("Found checkpoint TLS index: {index}", checkpointIndex);

            this.CheckpointFlagAddress = this.ReadMainTebPointer(checkpointIndex);
            this.logger.LogInformation("Found checkpoint flag: {offset}", this.CheckpointFlagAddress);
        }

        private unsafe void GetMainThreadInfo()
        {
            IntPtr handle = Win32.OpenProcess(AccessPermissions.ProcessQueryInformation | AccessPermissions.ProcessVmRead, false, this.ProcessId);
            if (handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var hinfBase = this.RemoteProcess.GetBaseOffset();
            var expectedThreadEntry = hinfBase + offsets.MainThreadEntry;

            var tinfo = new ThreadBasicInformation();
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

                this.RemoteProcess.ReadAt(tinfo.TebBaseAddress, out teb);

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
            this.RemoteProcess.ReadAt((mainThread.teb.TlsExpansionSlots + 8 * index - 0x200), out nint p);
            return p;
        }
    }
}
