using InfiniteTool.GameInterop.EngineDataTypes;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
    public class GameInstance : IDisposable
    {
        private const string LatestVersion = "6.10021.11755.0";
        private readonly IOffsetProvider offsetProvider;
        private readonly ILogger<GameInstance> logger;
        private InfiniteOffsets offsets = new InfiniteOffsets();
        private ArenaAllocator? allocator;
        private Dictionary<InfiniteMap, nint> scenarioStringLocations = new();

        public InfiniteOffsets GetCurrentOffsets() => offsets;

        public int ProcessId { get; private set; }

        public RpcRemoteProcess RemoteProcess { get; private set; }

        public Vector3 PlayerPosition { get; private set; }

        private (IntPtr handle, TEB teb) mainThread = (IntPtr.Zero, default);
        private nint CheckpointFlagAddress;
        private nint RevertFlagOffset;
        private nint PlayerDatumAddress;

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
            //this.RemoteProcess.Write(this.RevertFlagOffset, stackalloc byte[] { 0x22 });
            this.RemoteProcess.CallFunction<nint>(this.offsets.GameRevert);
        }

        internal void TriggerCheckpoint()
        {
            try
            {
                this.logger.LogInformation("Checkpoint requested");
                this.RemoteProcess.WriteAt(this.CheckpointFlagAddress, stackalloc byte[] { 0x3 });
                this.RemoteProcess.WriteAt(this.CheckpointFlagAddress + 4, stackalloc byte[] { 0x0 });
                this.RemoteProcess.WriteAt(this.CheckpointFlagAddress + 12, stackalloc byte[] { 0x3 });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Checkpoint failed, re-bootstrapping");
                this.Bootstrap();
            }
        }

        private CancellationTokenSource playerPoller = new();
        private Task? playerPollTask = null;
        private bool disposedValue;

        public void PollPlayerData()
        {
            if (this.playerPollTask != null)
            {
                this.playerPoller.Cancel();
                this.playerPollTask.Wait();
                this.playerPollTask = null;
            }

            if(this.playerPoller.IsCancellationRequested)
                this.playerPoller.TryReset();

            this.logger.LogInformation("Polling player data");

            this.playerPollTask = this.RemoteProcess.PollMemoryAt<PlayerDatum>(this.PlayerDatumAddress, 33, d =>
            {
                this.PlayerPosition = d.Position;
            }, playerPoller.Token);
        }

        public void StopPollingPlayerData()
        {
            this.playerPoller.Cancel();
            this.PlayerPosition = Vector3.Zero;
        }

        public void StartMap(InfiniteMap map)
        {
            if(this.scenarioStringLocations.TryGetValue(map, out var location))
            {
                this.logger.LogInformation("Starting map {map}//{location}", map, location);
                this.RemoteProcess.CallFunction<nint>(this.offsets.StartLevel, location);
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
                this.RemoteProcess.Attach(AttachGuard, "HaloInfinite");
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 0x5/*ACCESS_DENIED*/)
                {
                    App.RestartAsAdmin();
                }
            }
        }

        private bool AttachGuard(Process process)
        {
            if ((DateTime.Now - process.StartTime).TotalSeconds < 15)
                return false;

            var version = GetGameVersion(process);
            var tmpOffsets = this.offsetProvider.GetOffsets(version);

            if (tmpOffsets == InfiniteOffsets.Unknown)
            {
                MessageBox.Show($"Game Version {version} is not yet supported by this tool");
                return false; 
            }

            var found = TryGetMainThread(process, tmpOffsets, out var tid);

            if (process.HasExited) return false;
            return found;
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

            var proc = e.Process.Process;

            logger.LogInformation("Attaching to {exe}, version: {version}", proc?.MainModule?.FileName, proc?.MainModule?.FileVersionInfo.ToString());

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
            this.StopPollingPlayerData();
        }

        public void Bootstrap()
        {
            logger.LogInformation("Bootstrapping for new process {PID}", this.ProcessId);
            try
            {
                if (this.RemoteProcess.Process == null) throw new Exception("Remote process was not available");

                var offsets = LoadOffsets();
                Retry(() => GetMainThreadInfo(this.RemoteProcess.Process, offsets), times: 30, delay: 3000);
                PopulateAddresses();
                SetupWorkspace();

                PollPlayerData();
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

            var items = InteropConstantData.MapScenarios.ToArray();
            var strings = items.Select(i => i.Value).ToArray();

            var addresses = allocator.WriteStrings(strings);
            for(var i = 0; i < items.Length; i++)
            {
                scenarioStringLocations[items[i].Key] = addresses[i];
            }
        }

        public string GetGameVersion(Process? p = null)
        {
            var proc = p ?? this.RemoteProcess.Process;
            // TODO: winstore version on the exe is not available, need to find a reliable source of version info to not have latest version hardcoded for winstore fallback
            return proc?.MainModule?.FileVersionInfo.FileVersion ?? LatestVersion;
        }

        public InfiniteOffsets LoadOffsets()
        {
            this.offsets = this.offsetProvider.GetOffsets(GetGameVersion(this.RemoteProcess.Process));
            return this.offsets;
        }

        public unsafe void PopulateAddresses()
        {
            this.RevertFlagOffset = this.offsets.RevertFlagOffset;

            this.RemoteProcess.Read(this.offsets.Checkpoint_TlsIndexOffset, out int checkpointIndex);
            this.logger.LogInformation("Found checkpoint TLS index: {index}", checkpointIndex);

            this.CheckpointFlagAddress = this.ReadMainTebPointer(checkpointIndex);
            this.logger.LogInformation("Found checkpoint flag: {offset}", this.CheckpointFlagAddress);

            this.RemoteProcess.Read(this.offsets.PlayerDatum_TlsIndexOffset, out int playerDatumIndex);
            this.PlayerDatumAddress = this.ReadMainTebPointer(playerDatumIndex);
            this.logger.LogInformation("Found player datum: {offset}", this.PlayerDatumAddress);

        }

        private const string mainThreadDescription = "01 WORKER";
        private unsafe bool TryGetMainThread(Process process, InfiniteOffsets offsets, out uint threadId)
        {
            threadId = default;
            var remote = new PinvokeRemoteProcess(process.Id);

            uint? mainThreadId = null;

            if (offsets.ThreadTable.HasValue)
            {
                var threadTable = new GameThreadTableEntry[58];
                remote.Read(offsets.ThreadTable.Value, MemoryMarshal.AsBytes<GameThreadTableEntry>(threadTable));

                foreach (var entry in threadTable)
                {
                    var end = entry.Name;
                    while (*end != 00 && (end - entry.Name) < 32) end++;
                    var name = Encoding.UTF8.GetString(entry.Name, (int)(end - entry.Name));

                    if (name == mainThreadDescription)
                    {
                        mainThreadId = entry.ThreadId;
                        this.logger.LogInformation("Main thread found from table: {tid}", mainThreadId);
                        break;
                    }
                }
            }

            threadId = mainThreadId.GetValueOrDefault();
            return mainThreadId.HasValue;
        }

        private unsafe bool GetMainThreadInfo(Process process, InfiniteOffsets offsets)
        {
            IntPtr handle = Win32.OpenProcess(AccessPermissions.ProcessQueryInformation | AccessPermissions.ProcessVmRead, false, process.Id);
            if (handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (TryGetMainThread(process, offsets, out var mainThreadId))
            {
                this.logger.LogInformation("Main thread found, finding TEB");

                var thandle = Win32.OpenThread(ThreadAccess.QUERY_INFORMATION, false, mainThreadId);
                if (thandle == IntPtr.Zero)
                {
                    logger.LogError(new Win32Exception(Marshal.GetLastWin32Error()), "Error during thread scanning");
                }

                var tinfo = new ThreadBasicInformation();
                var hr = Win32.NtQueryInformationThread(thandle, ThreadInformationClass.ThreadBasicInformation, ref tinfo, Marshal.SizeOf(tinfo), out var tinfoReq);
                if (hr != 0)
                {
                    Win32.CloseHandle(handle);
                    Win32.CloseHandle(thandle);
                    logger.LogError(new Win32Exception(Marshal.GetLastWin32Error()), "Error during NtQueryInformationThread, ThreadBasicInformation query {hr}, req {tinfoReq}", hr, tinfoReq);
                    return false;
                }

                if (tinfo.TebBaseAddress == IntPtr.Zero)
                {
                    Win32.CloseHandle(handle);
                    Win32.CloseHandle(thandle);
                    logger.LogWarning($"TEB base address from discovered thread was zero, TID:{mainThreadId,5:x}");
                    return false;
                }

                logger.LogInformation($"Reading TEB for TID: {mainThreadId,5:x}, teb: {tinfo.TebBaseAddress}");

                TEB teb = default;
                this.RemoteProcess.ReadAt(tinfo.TebBaseAddress, out teb);

                mainThread = (thandle, teb);

                logger.LogInformation($"TID: {mainThreadId,5:x}, TEB: {tinfo.TebBaseAddress:x16}, TLS Expansion: {teb.TlsExpansionSlots:x16}");


                Win32.CloseHandle(handle); 
                Win32.CloseHandle(thandle);

                return true;
            }

            Win32.CloseHandle(handle);
            return false;
        }

        private unsafe uint? DiscoverMainThread(IntPtr handle)
        {
            var hinfBase = this.RemoteProcess.GetBaseOffset();
            var expectedThreadEntry = hinfBase + offsets.MainThreadEntry;

            uint? tid = null;

            foreach (var thread in this.RemoteProcess.Threads)
            {
                var thandle = Win32.OpenThread(ThreadAccess.QUERY_INFORMATION, false, (uint)thread.Id);

                if (thandle == IntPtr.Zero)
                {
                    logger.LogError(new Win32Exception(Marshal.GetLastWin32Error()), "Error during thread scanning");
                }

                var entry = new IntPtr();
                var hr = Win32.NtQueryInformationThread(thandle, ThreadInformationClass.ThreadQuerySetWin32StartAddress, ref entry, Marshal.SizeOf(entry), out var entryReq);
                if (hr != 0)
                {
                    Win32.CloseHandle(thandle);
                    logger.LogError(new Win32Exception(Marshal.GetLastWin32Error()), "Error during thread scanning, start address query {hr}", hr);
                    continue;
                }

                logger.LogInformation($"Inspecting TID: {thread.Id,5:x}, start: {entry:x}, expect: {expectedThreadEntry:x}");

                if (entry != expectedThreadEntry)
                {
                    Win32.CloseHandle(thandle);
                    continue;
                }

                tid = (uint)thread.Id;

                break;
            }

            return tid;
        }

        public nint ReadMainTebPointer(int index)
        {
            this.RemoteProcess.ReadAt((mainThread.teb.TlsExpansionSlots + 8 * index - 0x200), out nint p);
            return p;
        }

        private void Retry(Func<bool> action, int times = 5, int delay = 1000)
        {
            while(times > 0)
            {
                times--;

                try
                {
                    if (action())
                    {
                        return;
                    }
                    else
                    {
                        this.logger.LogWarning("Action did not complete");
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failure during action");

                    if(times <= 0)
                    {
                        throw;
                    }
                }

                this.logger.LogInformation("Retry {times}", times);

                Thread.Sleep(delay);
            }

            throw new Exception("Action did not complete successfully");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.allocator?.Dispose();
                    this.RemoteProcess.EjectMombasa();
                    this.RemoteProcess?.Dispose();
                    this.playerPoller?.Dispose();
                }

                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
        }
    }
}
