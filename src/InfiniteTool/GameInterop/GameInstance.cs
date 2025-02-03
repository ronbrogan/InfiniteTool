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
using System.IO;
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
        public InfiniteOffsets.Client Engine { get; private set; }
        private ArenaAllocator allocator;
        private Dictionary<InfiniteMap, nint> scenarioStringLocations = new();

        public InfiniteOffsets GetCurrentOffsets() => offsets;

        public int ProcessId { get; private set; }

        public RpcRemoteProcess RemoteProcess { get; private set; }
        private IGeneratedUtilities Utilities;

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

        public bool ProbablyIsInGame()
        {
            try
            {
                if (!Engine.IsInCampaignMenu())
                    return false;

                if (Engine.LoadScreenActive())
                    return false;

                if (this.InMainMenu())
                    return false;
            }
            catch { return false; }

            return true;
        }

        public bool InMainMenu()
        {
            return Engine.Level_IsCurrentNameEqual(this.RemoteProcess.GetBaseOffset() + this.offsets.MainMenuScenarioName);
        }

        public bool InCutscene()
        {
            try
            {
                if (!Engine.IsInCampaignMenu())
                    return false;

                if (Engine.LoadScreenActive())
                    return false;

                //if (Engine.composer_show_scene_is_playing())
                //    return true;

                //if (Engine.cinematic_in_progress())
                //    return true;

                if(Engine.ReadInCageFlag())
                    return true;
            }
            catch { return false; }

            return false;
        }

        public bool IsPaused()
        {
            try
            {
                this.RemoteProcess.ReadAt<GameTimeGlobals>(Engine.GetGameTimeGlobalsAddress(), out var timeGlobals);

                return timeGlobals.Suspended != 0;
            }
            catch { }

            return false;
        }

        public bool IsInTacMap()
        {
            var closed = Engine.ReadTacMapClosedFlag();
            return !closed;
        }

        internal async Task TriggerCheckpoint()
        {
            await Operation("Custom Checkpoint", () => Engine.game_save_fast());
        }

        internal async Task TriggerRevert()
        {
            await Operation("Reverting", () =>
            {
                var player = Engine.player_get(0);
                Engine.Object_SetObjectCannotTakeDamage(player, true);
                Engine.object_set_shield(player, 1f);
                Engine.game_revert();
            });
        }

        public async Task DoubleRevert()
        {
            await Operation("Double reverting", () =>
            {
                var player = Engine.player_get(0);
                Engine.Object_SetObjectCannotTakeDamage(player, true);
                Engine.object_set_shield(player, 1f);
                var cpInfo = Engine.ReadCheckpointInfo();
                cpInfo.CurrentSlot ^= 1;
                Engine.WriteCheckpointInfo(cpInfo);
                Engine.game_revert();
            });
        }

        internal async Task ToggleCheckpointSuppression()
        {
            await Operation("Toggling CP Suppression", () =>
            {
                var cpInfo = Engine.ReadCheckpointInfo();
                cpInfo.SuppressCheckpoints = (byte)(cpInfo.SuppressCheckpoints == 0 ? 1 : 0);
                this.logger.LogInformation("Checkpoint suppresion toggle to {enabled}", cpInfo.SuppressCheckpoints);
                Engine.WriteCheckpointInfo(cpInfo);
                ShowMessage(cpInfo.SuppressCheckpoints == 1
                    ? "   Suppression [ON]"
                    : "   Suppression [OFF]");
            });
        }

        internal bool CheckpointsSuppressed()
        {
            var cpInfo = Engine.ReadCheckpointInfo();
            return cpInfo.SuppressCheckpoints == 1;
        }

        internal async Task ToggleInvuln()
        {
            await Operation("Toggling Invuln", () =>
            {
                var invuln = PlayerIsInvulnerable() ? 0 : 1;
                this.logger.LogInformation($"Invuln requested, val: {invuln}");
                var player = Engine.player_get(0);
                Engine.Object_SetObjectCannotTakeDamage(player, invuln == 1);
                ShowMessage($" Invuln [{(invuln == 1 ? "ON" : "OFF")}]");
            });
        }

        internal bool PlayerIsInvulnerable()
        {
            var player = Engine.player_get(0);
            return Engine.Object_GetObjectCannotTakeDamage(player);
        }

        public async Task RestockPlayer()
        {
            await Operation("Restocking player", () =>
            {
                var player = Engine.player_get(0);
                Engine.Unit_RefillAmmo(player);
                Engine.Unit_RefillGrenades(player, 1);
                Engine.Unit_RefillGrenades(player, 2);
                Engine.Unit_RefillGrenades(player, 3);
                Engine.Unit_RefillGrenades(player, 4);
            });
        }

        public async Task UnlockAllEquipment()
        {
            await Operation("Unlocking All Equipment", () =>
            {
                var stringAddr = this.allocator.Allocate(8);
                var resultAddr = this.allocator.Allocate(8);

                var valAddr = this.allocator.Allocate(1);

                RemoteProcess.WriteAt(valAddr, (byte)255);
                UnlockEquipment(stringAddr, resultAddr, valAddr);

                RemoteProcess.WriteAt(valAddr, (byte)255);
                NoticeDeadSpartans(stringAddr, resultAddr, valAddr);
                    
            });

            void UnlockEquipment(nint stringAddr, nint resultAddr, nint valAddr)
            {
                foreach (var k in PersistenceKeys.EquipmentKeys)
                {
                    var keyAddr = this.allocator.WriteString(k);
                    RemoteProcess.WriteAt(stringAddr, keyAddr);
                    Engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);
                    Engine.Persistence_SetBoolKey(0, resultAddr, valAddr);
                }
            }

            void NoticeDeadSpartans(nint stringAddr, nint resultAddr, nint valAddr)
            {
                foreach (var k in PersistenceKeys.SpartanKeys)
                {
                    var keyAddr = this.allocator.WriteString(k);
                    RemoteProcess.WriteAt(stringAddr, keyAddr);
                    Engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);
                    Engine.Persistence_SetByteKey(0, resultAddr, valAddr);
                }
            }
        }

        public async Task ResetAllEquipment()
        {
            await Operation("Resetting Equipment Levels", () =>
            {
                var stringAddr = this.allocator.Allocate(8);
                var resultAddr = this.allocator.Allocate(8);

                var valAddr = this.allocator.Allocate(1);
                RemoteProcess.WriteAt(valAddr, (byte)0);

                foreach(var k in PersistenceKeys.EquipmentLevels)
                {
                    var keyAddr = this.allocator.WriteString(k);
                    RemoteProcess.WriteAt(stringAddr, keyAddr);
                    Engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);
                    Engine.Persistence_SetByteKey(0, resultAddr, valAddr);
                }
            });
        }

        public async Task SetEquipmentPoints(int value)
        {
            await Operation($"Setting Equipment Points to {value}", () =>
            {
                var stringAddr = this.allocator.Allocate(8);
                var resultAddr = this.allocator.Allocate(8);
                var valAddr = this.allocator.Allocate(sizeof(int));
                RemoteProcess.WriteAt(valAddr, value);

                var keyAddr = this.allocator.WriteString("Equipment_Points");
                RemoteProcess.WriteAt(stringAddr, keyAddr);

                Engine.Persistence_TryCreateKeyFromString(0, resultAddr, stringAddr);
                Engine.Persistence_SetLongKey(0, resultAddr, valAddr);
            });
        }

        public async Task SpawnWeapon(TagInfo weapon)
        {
            // Our func takes these 'global' tag IDs, but will only succeed if they're
            // available in the 'tag translation table' (as I'm calling it)
            // Need to scan this table and build up a list of available weapons to create
            // Also need to figure out how variants really work :)
            // ObjectGetVariant[228] Object->StringId      00aa553c // 00aa54fc
            // object_set_variant[236] Object,StringId->Void   00f17524 // 00aa6f00

            var name = Path.GetFileNameWithoutExtension(weapon.Name);

            await Operation($"Weapon Requested: {name}", () =>
            {
                var player = Engine.player_get(0);
                var created = Engine.Object_PlaceTagAtObjectLocation(weapon.Id, player);

                if ((int)created == -1)
                    ShowMessage("failed to spawn weap");
                else
                    ShowMessage($"   spawned {name}");
            });
        }

        public async Task SpawnVehicle(TagInfo vehicle)
        {
            // Our func takes these 'global' tag IDs, but will only succeed if they're
            // available in the 'tag translation table' (as I'm calling it)
            // Need to scan this table and build up a list of available weapons to create
            // Also need to figure out how variants really work :)
            // ObjectGetVariant[228] Object->StringId      00aa553c // 00aa54fc
            // object_set_variant[236] Object,StringId->Void   00f17524 // 00aa6f00

            var name = Path.GetFileNameWithoutExtension(vehicle.Name);

            await Operation($"Vehicle Requested: {name}", () =>
            {
                var player = Engine.player_get(0);
                var created = Engine.Object_PlaceTagAtObjectLocation(vehicle.Id, player);

                if ((int)created == -1)
                    ShowMessage("failed to spawn vehi");
                else
                    ShowMessage($"   spawned {name}");
            });
        }

        public async Task SpawnBiped(TagInfo character)
        {
            // Our func takes these 'global' tag IDs, but will only succeed if they're
            // available in the 'tag translation table' (as I'm calling it)
            // Need to scan this table and build up a list of available weapons to create
            // Also need to figure out how variants really work :)
            // ObjectGetVariant[228] Object->StringId      00aa553c // 00aa54fc
            // object_set_variant[236] Object,StringId->Void   00f17524 // 00aa6f00

            var name = Path.GetFileNameWithoutExtension(character.Name);

            await Operation($"Biped Requested: {name}", () =>
            {
                var player = Engine.player_get(0);
                var created = Engine.Object_PlaceTagAtObjectLocation(character.Id, player);

                if ((int)created == -1)
                    ShowMessage("failed to spawn biped");
                else
                    ShowMessage($"   spawned {name}");
            });
        }

        internal async Task ToggleGameTimePause()
        {
            var pauseState = GameTimeIsPaused();
            await Operation(!pauseState ? "Freezing time" : "Thawing time", () =>
            {
                Engine.Game_TimeSetPaused(!pauseState);
            });
        }

        internal bool GameTimeIsPaused()
        {
            this.RemoteProcess.ReadAt<GameTimeGlobals>(Engine.GetGameTimeGlobalsAddress(), out var timeGlobals);

            return timeGlobals.Suspended == 0x0040;
        }

        internal async Task ToggleAi()
        {
            var enable = AiDisabled();
            await Operation(enable ? "AI enabled" : "AI disabled", () =>
            {
                Engine.ai_enable(enable);
            });
        }

        internal bool AiDisabled()
        {
            return !Engine.ai_enabled();
        }

        internal async Task NukeAi()
        {
            await Operation("Dropping nuke", async () =>
            {
                await Task.Delay(500);
                Engine.ai_kill_all();
            });
        }

        internal async Task ToggleSkull(string name, int id)
        {
            await Operation($"Toggling skull {name}", async () =>
            {
                var en = this.Engine.is_skull_active(id);
                this.Engine.skull_enable(id, !en);
                ShowMessage($"   [{(!en ? "ON" : "OFF")}]");
            });
        }

        internal bool GetSkullState(int id)
        {
            return Engine.is_skull_active(id);
        }

        public void ShowMessage(string message)
        {
            var playerIdThing = 0xEC700000;

            var stringAddress = this.allocator.WriteString(message, Encoding.Unicode);

            this.Utilities.ShowMessage(playerIdThing, stringAddress, 5f);
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
                //MessageBox.Show($"Game Version {version} is not yet supported by this tool");
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
            this.BeforeDetach?.Invoke(this, new EventArgs());
            logger.LogInformation("Detaching from process {PID}", this.ProcessId);
            this.ProcessId = 0;
            this.RevertFlagOffset = 0;
            this.CheckpointFlagAddress = 0;
        }

        public void Bootstrap()
        {
            logger.LogInformation("Bootstrapping for new process {PID}", this.ProcessId);
            if (this.RemoteProcess.Process == null) throw new Exception("Remote process was not available");

            try
            {
                this.offsets = LoadOffsets();
                this.Engine = offsets.CreateClient(this.RemoteProcess);

                Retry(() => GetMainThreadInfo(this.RemoteProcess.Process, offsets), times: 30, delay: 3000);

                SetupWorkspace();

                Utilities = Codegen.Generate(this.offsets, this.RemoteProcess);
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

        object prepLock = new();
        nint[] previousTlsValues = Array.Empty<nint>();
        public void PrepareForScriptCalls()
        {
            lock(prepLock)
            {
                SetupStaticTls();
                SyncTlsSlots();
            }
        }

        private void SetupStaticTls()
        {
            // Update checked values in static thread local storage
            var tlp = this.RemoteProcess.GetThreadLocalPointer();
            var magic = this.Engine.ReadThreadLocalStaticInitializer();

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
                var newVal = newVals[i];

                if(previousTlsValues.Length == 0 || newVal != previousTlsValues[i])
                {
                    this.RemoteProcess.SetTlsValue(i, newVal);
                }
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

        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.allocator?.Dispose();
                    this.RemoteProcess.EjectMombasa();
                    this.RemoteProcess?.Dispose();
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

        private async Task Operation(string message, Action func, string? log = null)
        {
            using var l = await StartExclusiveOperation();

            log ??= message;
            this.logger.LogInformation($"UserOperation: {log}");
            this.PrepareForScriptCalls();
            var success = false;

            try
            {
                this.ShowMessage(message);
                func();
                success = true;
            }
            catch(Exception e)
            {
                try
                {
                    this.ShowMessage($"Failure during {message}, check logs");
                }
                catch { }

                this.logger.LogError(e, $"Failure during {message}");
            }
            finally
            {                
                this.allocator.Reclaim(zero: true);
            }
        }

        private async Task Operation(string message, Func<Task> func, string? log = null)
        {
            log ??= message;
            this.logger.LogInformation($"UserOperation: {log}");
            this.PrepareForScriptCalls();
            var success = false;

            try
            {
                this.ShowMessage(message);
                await func();
                success = true;
            }
            catch (Exception e)
            {
                try
                {
                    this.ShowMessage($"Failure during {message}, check logs");
                }
                catch { }

                this.logger.LogError(e, $"Failure during {message}");
            }
            finally
            {
                this.allocator.Reclaim(zero: true);
            }
        }

        internal async Task ForceSkipCutscene()
        {
            // small delay to avoid skipping too close to the start of the cutscene
            await Task.Delay(60);
            await Operation("Skipping CS", () => this.Engine.composer_debug_cinematic_skip(), "Skip cutscene requested");
        }

        internal void ResetOrLaunchMap(uint? loadedMapId, uint? loadedSpawnId)
        {
            if (!loadedMapId.HasValue || loadedMapId.Value <= 0)
                return;

            var state = Engine.ResolveGameStateStuff();
            this.RemoteProcess.ReadAt<uint>(state + 0xe887c, out var curr);

            if (curr == loadedMapId)
            {
                Engine.map_reset();
            }
            else
            {
                Engine.RestartMission();
            }
        }

        internal async Task TogglePlayerNoClip()
        {
            var val = PlayerNoClip();
            await Operation($"Noclip [{(!val ? "ON" : "OFF")}]", () =>
            {
                var player = Engine.player_get(0);
                Engine.object_set_physics(player, val);
            });
        }

        internal bool PlayerNoClip()
        {
            var player = (int)Engine.player_get(0);
            if (player == -1)
                return false;

            var objAddr = Engine.ResolveObjectPointer(player);

            if (objAddr == 0)
                return false;

            this.RemoteProcess.ReadAt<nint>(objAddr + 0xf0, out var flags);
            return (flags & 0x100) != 0;
        }

        internal async Task ToggleSlowMo()
        {
            var val = SlowMoActivated();
            await Operation($"SlowMo [{(!val ? "ON" : "OFF")}]", () =>
            {
                var addr = Engine.GetGameTimeGlobalsAddress();
                this.RemoteProcess.WriteAt<float>(addr + GameTimeGlobals.TickRateReciprocalOffset, val ? 1f/60f : 1f / 120f);
            });
        }

        internal bool SlowMoActivated()
        {
            this.RemoteProcess.ReadAt<GameTimeGlobals>(Engine.GetGameTimeGlobalsAddress(), out var timeGlobals);
            return timeGlobals.TickRateReciprocal < 0.016f;
        }

        internal async Task ToggleFastMo()
        {
            var val = FastMoActivated();
            await Operation($"FastMo [{(!val ? "ON" : "OFF")}]", () =>
            {
                var addr = Engine.GetGameTimeGlobalsAddress();
                this.RemoteProcess.WriteAt<float>(addr + GameTimeGlobals.GameTimeMultiplierOffset, val ? 1 : 3);
            });
        }

        internal bool FastMoActivated()
        {
            this.RemoteProcess.ReadAt<GameTimeGlobals>(Engine.GetGameTimeGlobalsAddress(), out var timeGlobals);
            return timeGlobals.GameTimeMultiplier > 1;
        }

        internal async Task ToggleCoords()
        {
            var val = CoordsOn();
            await Operation($"Pancam [{(!val ? "ON" : "OFF")}]", () =>
            {
                var start = this.Engine.GetGameStateFlagsBase();
                this.RemoteProcess.WriteAt<byte>(start + this.offsets.CheatsEnabledOffset, 1);

                var flags = this.Engine.ResolveCheatFlagsLocation();
                this.RemoteProcess.WriteAt<byte>(flags + this.offsets.PancamEnabledOffset, val ? (byte)0 : (byte)1);
            });
        }

        internal bool CoordsOn()
        {
            if(!this.Engine.CheatsAreEnabled())
                return false;

            var flags = this.Engine.ResolveCheatFlagsLocation();
            if (flags == 0)
                return false;

            this.RemoteProcess.ReadAt<byte>(flags + this.offsets.PancamEnabledOffset, out var val);
            return val != 0;
        }

        internal void ChangeDifficulty(string v)
        {
            _ = Operation($"Diff -> {v}", async () =>
            {
                var diff = v switch
                {
                    "easy" => 0,
                    "normal" => 1,
                    "heroic" => 2,
                    "legendary" => 3,
                    _ => throw new NotImplementedException()
                };

                // game difficulty change will not take effect unless the game is loading, so revert first
                this.Engine.map_reset();
                await Task.Delay(300);
                this.Engine.game_difficulty(diff);
            });
        }

        internal async Task ToggleFlyCam()
        {
            var val = FlycamEnabled();
            await Operation($"Flycam [{(!val ? "ON" : "OFF")}]", () =>
            {
                var start = this.Engine.GetGameStateFlagsBase();
                this.RemoteProcess.WriteAt<byte>(start + this.offsets.CheatsEnabledOffset, 1);

                var flags = this.Engine.ResolveCheatFlagsLocation();
                this.RemoteProcess.WriteAt<byte>(flags + this.offsets.FlycamEnabledOffset, val ? (byte)0 : (byte)1);
            });
        }

        internal bool FlycamEnabled()
        {
            return this.Engine.FlycamIsEnabled();
        }


        SemaphoreSlim operationSemaphore = new SemaphoreSlim(1, 1);
        internal async Task<IDisposable> StartExclusiveOperation()
        {
            await operationSemaphore.WaitAsync();

            return new OperationScope(operationSemaphore);
        }

        private class OperationScope : IDisposable
        {
            private SemaphoreSlim operationSemaphore;

            public OperationScope(SemaphoreSlim operationSemaphore)
            {
                this.operationSemaphore = operationSemaphore;
            }

            public void Dispose()
            {
                operationSemaphore.Release();
            }
        }
    }
}
