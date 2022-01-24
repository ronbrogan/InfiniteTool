using InfiniteTool.GameInterop.EngineDataTypes;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace InfiniteTool.GameInterop
{
    [AddINotifyPropertyChangedInterface]
    public class CheckpointData
    {
        public CheckpointData(string levelName, TimeSpan gameTime, byte[] checkpointData, string filename)
        {
            this.LevelName = levelName;
            this.GameTime = gameTime;
            this.Data = checkpointData;
            this.Filename = filename;
        }

        public string LevelName { get; set; }
        public TimeSpan GameTime { get; set; }
        public byte[] Data { get; set; }
        public string Filename { get; set; }
    }

    [AddINotifyPropertyChangedInterface]
    public class GameInstance
    {
        private readonly IOffsetProvider offsetProvider;
        private readonly ILogger<GameInstance> logger;
        private InfiniteOffsets offsets = new InfiniteOffsets();
        private ArenaAllocator? allocator;
        private Dictionary<InfiniteMap, nint> scenarioStringLocations = new();

        public InfiniteOffsets GetCurrentOffsets() => offsets;

        public RpcRemoteProcess RemoteProcess { get; }

        private (IntPtr handle, TEB teb) mainThread = (IntPtr.Zero, default);

        public int ProcessId { get; private set; }

        public nint CheckpointFlagAddress;
        public nint RevertFlagOffset;

        public event EventHandler<EventArgs>? OnAttach;
        public event EventHandler<EventArgs>? BeforeDetach;

        public GameInstance(IOffsetProvider offsetProvider, ILogger<GameInstance> logger)
        {
            this.offsetProvider = offsetProvider;
            this.logger = logger;
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

        public void StartMap(InfiniteMap map)
        {
            if(this.scenarioStringLocations.TryGetValue(map, out var location))
            {
                this.RemoteProcess.CallFunction<nint>(this.offsets.StartMap, location);
            }
        }

        public const int CheckpointDataSize = 0xF0000; //0xEB9E8;
        public unsafe byte[]? SaveCheckpoint()
        {
            this.RemoteProcess.Read(this.offsets.CheckpointInfoOffset, out CheckpointInfo cpInfo);

            var cpData = new byte[CheckpointDataSize+4];

            if (cpInfo.Slot0 == 0 || cpInfo.Slot1 == 0) return null;

            var hash = cpInfo.CurrentSlot == 0 ? cpInfo.Hash0 : cpInfo.Hash1;
            BitConverter.TryWriteBytes(cpData, hash);

            var slotAddress = cpInfo.CurrentSlot == 0 ? cpInfo.Slot0 : cpInfo.Slot1;
            this.RemoteProcess.ReadAt(slotAddress, cpData.AsSpan().Slice(4));

            return cpData;
        }

        internal void InjectCheckpoint(byte[] data)
        {
            if (data.Length == 0) return;

            this.RemoteProcess.Read(this.offsets.CheckpointInfoOffset, out CheckpointInfo cpInfo);

            if (cpInfo.Slot0 == 0 || cpInfo.Slot1 == 0) return;

            nint slotAddress;

            if(cpInfo.CurrentSlot == 0)
            {
                slotAddress = cpInfo.Slot0;
                cpInfo.Hash0 = BitConverter.ToUInt32(data, 0);
            }
            else
            {
                slotAddress = cpInfo.Slot1;
                cpInfo.Hash1 = BitConverter.ToUInt32(data, 0);
            }

            this.RemoteProcess.WriteAt(slotAddress, data.AsSpan().Slice(4));
            this.RemoteProcess.WriteAt(cpInfo.SlotX, data.AsSpan().Slice(4));

            this.RemoteProcess.Write(this.offsets.CheckpointInfoOffset, cpInfo);
        }

        internal void ToggleCheckpointSuppression()
        {
            this.RemoteProcess.Read(this.offsets.CheckpointInfoOffset, out CheckpointInfo cpInfo);
            cpInfo.SuppressCheckpoints = (byte)(cpInfo.SuppressCheckpoints == 0 ? 1 : 0);
            this.logger.LogInformation("Checkpoint suppresion toggle to {enabled}", cpInfo.SuppressCheckpoints);
            this.RemoteProcess.Write(this.offsets.CheckpointInfoOffset, cpInfo);
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
                LoadOffsets();
                GetMainThreadInfo();
                PopulateAddresses();
                SetupWorkspace();
            }
            catch { }
        }

        private void SetupWorkspace()
        {
            if (this.allocator != null)
            {
                try
                {
                    lock (this.allocator)
                    {
                        this.allocator.Dispose();
                    }
                }
                catch { }
            }

            var allocator = new ArenaAllocator(this.RemoteProcess, 4 * 1024 * 1024); // 4MB working area

            foreach(var (map,scenario) in InteropConstantData.MapScenarios)
            {
                scenarioStringLocations[map] = allocator.WriteString(scenario);
            }
        }

        public void LoadOffsets()
        {
            var version = this.RemoteProcess.Process?.MainModule?.FileVersionInfo.FileVersion;

            this.offsets = this.offsetProvider.GetOffsets(version);
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
