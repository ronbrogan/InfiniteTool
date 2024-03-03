using Google.Protobuf.WellKnownTypes;
using InfiniteTool.GameInterop.EngineDataTypes;
using InfiniteTool.GameInterop.Internal;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core;
using Superintendent.Core.Native;
using Superintendent.Core.Remote;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
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
        private const string LatestVersion = "6.10021.12835.0";
        private readonly IOffsetProvider offsetProvider;
        private readonly ILogger<GameInstance> logger;
        private InfiniteOffsets offsets = new InfiniteOffsets();
        private InfiniteOffsets.Client engine;
        private ArenaAllocator? allocator;
        private Dictionary<InfiniteMap, nint> scenarioStringLocations = new();

        public InfiniteOffsets GetCurrentOffsets() => offsets;

        public int ProcessId { get; private set; }

        public RpcRemoteProcess RemoteProcess { get; private set; }

        public Vector3 PlayerPosition { get; private set; }

        private (IntPtr handle, ThreadBasicInformation tinfo, TEB teb) mainThread = (IntPtr.Zero, default, default);
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

        private bool susp = false;

        internal void TriggerCheckpoint()
        {
            if(susp)
            {
                this.RemoteProcess.ResumeAppThreads();
                susp = false;
                return;
            }

            try
            {
                this.logger.LogInformation("Checkpoint requested");
                this.PrepareForScriptCalls();
                ShowMessage("Custom Checkpoint");
                engine.game_save_fast();
                this.RemoteProcess.SuspendAppThreads();
                susp = true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Checkpoint call failed, but might have worked?");
            }
        }

        internal void TriggerRevert()
        {
            this.logger.LogInformation("Revert requested");
            this.PrepareForScriptCalls();
            ShowMessage("Reverting");
            var player = engine.player_get(0);
            engine.object_set_shield(player, 1f);
            engine.game_revert();
        }

        public void DoubleRevert()
        {
            this.logger.LogInformation("Double revert requested");
            this.PrepareForScriptCalls();
            ShowMessage("Double reverting");
            var player = engine.player_get(0);
            engine.object_set_shield(player, 1f);
            var cpInfo = engine.ReadCheckpointInfo();
            cpInfo.CurrentSlot ^= 1;
            engine.WriteCheckpointInfo(cpInfo);
            engine.game_revert();
        }

        internal void ToggleCheckpointSuppression()
        {
            this.PrepareForScriptCalls();
            var cpInfo = engine.ReadCheckpointInfo();
            cpInfo.SuppressCheckpoints = (byte)(cpInfo.SuppressCheckpoints == 0 ? 1 : 0);
            ShowMessage(cpInfo.SuppressCheckpoints == 1
                ? "Suppressing checkpoints..."
                : "Allowing checkpoints...");

            this.logger.LogInformation("Checkpoint suppresion toggle to {enabled}", cpInfo.SuppressCheckpoints);
            engine.WriteCheckpointInfo(cpInfo);
        }

        private int invuln = 0;
        internal void ToggleInvuln()
        {
            this.logger.LogInformation($"Invuln requested, val: {invuln}");
            this.PrepareForScriptCalls();
            invuln ^= 1;
            ShowMessage($"Toggling invuln to {invuln == 1}");
            var player = RemoteProcess.CallFunction<nint>(this.offsets.player_get, 0).Item2;
            
            RemoteProcess.CallFunction<nint>(this.offsets.Object_SetObjectCannotTakeDamage, player, invuln);
        }

        public void RestockPlayer()
        {
            this.logger.LogInformation("Restock requested");
            this.PrepareForScriptCalls();
            ShowMessage($"Restocking player");

            var player = engine.player_get(0);

            // Restocks throw for some reason, but they *do* restock 
            engine.Unit_RefillAmmo(player);
            engine.Unit_RefillGrenades(player, 1);
            engine.Unit_RefillGrenades(player, 2);
            engine.Unit_RefillGrenades(player, 3);
            engine.Unit_RefillGrenades(player, 4);
        }

        public void UnlockAllEquipment()
        {
            this.logger.LogInformation("Equipment unlock requested");
            this.PrepareForScriptCalls();

            ShowMessage($"Unlocking all equipment");

            var stringAddr = this.allocator.Allocate(8);
            var resultAddr = this.allocator.Allocate(8);

            var valAddr = this.allocator.Allocate(1);
            RemoteProcess.WriteAt(valAddr, (byte)1);

            UnlockEquipment("schematic_evade");
            UnlockEquipment("schematic_wall");
            UnlockEquipment("schematic_sensor");
            UnlockEquipment("Schematic-ShieldUpdgrade1");
            UnlockEquipment("Schematic-ShieldUpdgrade2");
            UnlockEquipment("Schematic-ShieldUpdgrade3");
            UnlockEquipment("grapple_hook");
            

            RemoteProcess.WriteAt(valAddr, (byte)1);
            NoticeDeadSpartan("spartan_griffin");
            NoticeDeadSpartan("spartan_makovich");
            NoticeDeadSpartan("spartan_sorel");
            NoticeDeadSpartan("spartan_horvath");
            NoticeDeadSpartan("spartan_vettel");
            NoticeDeadSpartan("spartan_stone");
            NoticeDeadSpartan("spartan_kovan");
            NoticeDeadSpartan("spartan_stone");

            void UnlockEquipment(string identifier)
            {
                var keyAddr = this.allocator.WriteString(identifier);
                RemoteProcess.WriteAt(stringAddr, keyAddr);

                engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);

                RemoteProcess.ReadAt<uint>(resultAddr, out var persistenceKey);

                engine.Persistence_SetBoolKey(0, resultAddr, valAddr);
            }

            void NoticeDeadSpartan(string identifier)
            {
                var keyAddr = this.allocator.WriteString(identifier);
                RemoteProcess.WriteAt(stringAddr, keyAddr);

                engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);

                RemoteProcess.ReadAt<uint>(resultAddr, out var persistenceKey);

                engine.Persistence_SetByteKey(0, resultAddr, valAddr);
            }
        }

        public void ResetAllEquipment()
        {
            this.logger.LogInformation("Equipment reset requested");
            this.PrepareForScriptCalls();
            ShowMessage($"Resetting equipment levels");

            var stringAddr = this.allocator.Allocate(8);
            var resultAddr = this.allocator.Allocate(8);

            var valAddr = this.allocator.Allocate(1);
            RemoteProcess.WriteAt(valAddr, (byte)0);

            SetEquipmentUpgrade("Grapple_Upgrade_Level");
            SetEquipmentUpgrade("Evade_Upgrade_Level");
            SetEquipmentUpgrade("Wall_Upgrade_Level");
            SetEquipmentUpgrade("Sensor_Upgrade_Level");
            SetEquipmentUpgrade("Shield_Upgrade_Level");

            void SetEquipmentUpgrade(string identifier)
            {
                var keyAddr = this.allocator.WriteString(identifier);
                RemoteProcess.WriteAt(stringAddr, keyAddr);

                engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);

                RemoteProcess.ReadAt<uint>(resultAddr, out var persistenceKey);

                engine.Persistence_SetByteKey(0, resultAddr, valAddr);
            }
        }

        public void SetEquipmentPoints(int value)
        {
            this.logger.LogInformation("equipment points requested");
            this.PrepareForScriptCalls();
            ShowMessage($"Setting equipment points to {value}");

            //var player = RemoteProcess.CallFunction<nint>(this.offsets.player_get, 0).Item2;
            //var participant = RemoteProcess.CallFunction<nint>(this.offsets.unit_get_player, player).Item2;

            var stringAddr = this.allocator.Allocate(8);
            var resultAddr = this.allocator.Allocate(8);
            var valAddr = this.allocator.Allocate(sizeof(int));
            RemoteProcess.WriteAt(valAddr, value);

            var keyAddr = this.allocator.WriteString("Equipment_Points");
            RemoteProcess.WriteAt(stringAddr, keyAddr);

            engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);

            RemoteProcess.ReadAt<uint>(resultAddr, out var persistenceKey);

            engine.Persistence_SetLongKey(0, resultAddr, valAddr);
        }

        public bool SpawnWeapon(TagInfo weapon)
        {
            // Our func takes these 'global' tag IDs, but will only succeed if they're
            // available in the 'tag translation table' (as I'm calling it)
            // Need to scan this table and build up a list of available weapons to create
            // Also need to figure out how variants really work :)
            // ObjectGetVariant[228] Object->StringId      00aa553c // 00aa54fc
            // object_set_variant[236] Object,StringId->Void   00f17524 // 00aa6f00


            this.logger.LogInformation("Weapon requested");
            this.PrepareForScriptCalls();

            ShowMessage("Weapon spawned");

            var player = engine.player_get(0);
            var created = engine.Object_PlaceTagAtObjectLocation(weapon.Id, player);

            return created != -1;
        }

        public void ShowMessage(string message)
        {
            nint playerIdThing = (nint)0xEC700000;

            nint getMessageBuffer = 0x13bf710;
            nint getMessageBufferSlot = 0x13bee30;
            nint showMessage = 0x13c1ef0;

            var stringAddress = this.allocator.WriteString(message, Encoding.Unicode);

            var bufAddress = this.RemoteProcess.CallFunction<nint>(getMessageBuffer, 0).Item2;

            var slotAddress = this.RemoteProcess.CallFunction<nint>(getMessageBufferSlot, bufAddress + 0x888, playerIdThing, playerIdThing).Item2;

            var duration = BitConverter.SingleToUInt32Bits(5f);

            _ = this.RemoteProcess.CallFunction<nint>(showMessage, bufAddress, slotAddress, (nint)duration, stringAddress);

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
            if (this.RemoteProcess.Process == null) throw new Exception("Remote process was not available");

            try
            {
                

                this.offsets = LoadOffsets();
                this.engine = offsets.CreateClient(this.RemoteProcess);

                Retry(() => GetMainThreadInfo(this.RemoteProcess.Process, offsets), times: 30, delay: 3000);
                //PopulateAddresses();
                SetupWorkspace();

                

                //PollPlayerData();
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

            this.allocator = new ArenaAllocator(this.RemoteProcess, 4 * 1024 * 1024); // 4MB working area
        }

        public string GetGameVersion(Process? p = null)
        {
            var proc = p ?? this.RemoteProcess.Process;
            // TODO: winstore version on the exe is not available, need to find a reliable source of version info to not have latest version hardcoded for winstore fallback
            
            // IDEA: find location of version strings for each version, iterate over known and read, check version string equality
            
            return proc?.MainModule?.FileVersionInfo.FileVersion ?? LatestVersion;
        }

        public InfiniteOffsets LoadOffsets()
        {
            this.offsets = this.offsetProvider.GetOffsets(GetGameVersion(this.RemoteProcess.Process));
            return this.offsets;
        }


        private const string mainThreadDescription = "00 MAIN";
        private unsafe bool TryGetMainThread(Process process, InfiniteOffsets offsets, out uint threadId)
        {
            threadId = default;
            var remote = new PinvokeRemoteProcess(process.Id);

            uint? mainThreadId = null;

            
            var threadTable = new GameThreadTableEntry[58];
            remote.ReadPointerSpan(offsets.ThreadTable, threadTable);

            foreach (var entry in threadTable)
            {
                var end = entry.Name;
                while (*end != 00 && (end - entry.Name) < 32) end++;
                var name = Encoding.UTF8.GetString(entry.Name, (int)(end - entry.Name));

                if (name == mainThreadDescription)
                {
                    threadId = entry.ThreadId;
                    this.logger.LogInformation("Main thread found from table: {tid}", mainThreadId);
                    return true;
                }
            }
            

            threadId = default;
            return false;
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

                mainThread = (thandle, tinfo, teb);

                logger.LogInformation($"TID: {mainThreadId,5:x}, TEB: {tinfo.TebBaseAddress:x16}, TLS Expansion: {teb.TlsExpansionSlots:x16}");


                Win32.CloseHandle(handle); 
                Win32.CloseHandle(thandle);

                return true;
            }

            Win32.CloseHandle(handle);
            return false;
        }

        nint[] previousTlsValues = Array.Empty<nint>();
        public void PrepareForScriptCalls()
        {
            SetupStaticTls();

            SyncTlsSlots();
        }

        private void SetupStaticTls()
        {
            // Update checked values in static thread local storage
            var tlp = this.RemoteProcess.GetThreadLocalPointer();
            var magic = this.engine.ReadThreadLocalStaticInitializer();

            this.RemoteProcess.ReadAt(tlp, out nint tlMem);
            this.RemoteProcess.WriteAt(tlMem + 32, magic);
            this.RemoteProcess.WriteAt<byte>(tlMem + 320, 1);
            this.RemoteProcess.WriteAt<byte>(tlMem + 325, 0);
        }

        private unsafe void SyncTlsSlots()
        {
            const int start = 16; // skip first X values, might need to adjust
            const int normalSlotCount = 64;
            const int expansionSlotStart = normalSlotCount;
            const int expansionSlotCount = 1024;
            const int totalCount = normalSlotCount + expansionSlotCount;

            Span<nint> newVals = stackalloc nint[totalCount];

            // Grab lower 64 (reading new TEB here instead of using mainThread.teb since
            // it could have gotten out of date since it was first read)
            this.RemoteProcess.ReadAt<TEB>(mainThread.tinfo.TebBaseAddress, out var teb);
            for (var i = 0; i < 64; i++)
            {
                newVals[i] = (nint)teb.TlsSlots[i];
            }

            // Grab expansion slots
            var byteDest = MemoryMarshal.Cast<nint, byte>(newVals.Slice(expansionSlotStart));
            this.RemoteProcess.ReadSpanAt(mainThread.teb.TlsExpansionSlots, byteDest);

            // Don't write if we don't need to
            if (newVals.SequenceEqual(previousTlsValues))
                return;

            for (var i = start; i < totalCount; i++)
            {
                this.RemoteProcess.SetTlsValue(i, newVals[i]);
            }

            previousTlsValues = newVals.ToArray();
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

        private int pauseState = 0;
        internal void TogglePause()
        {
            this.PrepareForScriptCalls();
            pauseState ^= 1;
            ShowMessage(pauseState == 1 ? "Freezing time" : "Thawing time");
            engine.Game_TimeSetPaused(pauseState == 1);
        }

        private int aiState = 0;
        internal void ToggleAi()
        {
            this.PrepareForScriptCalls();

            aiState ^= 1;

            ShowMessage(aiState == 1 ? "AI enabled" : "AI disabled");
            engine.ai_enable(aiState == 1);
        }
    }
}
