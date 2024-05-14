using DynamicData;
using InfiniteTool.GameInterop.EngineDataTypes;
using InfiniteTool.GameInterop.Internal;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfiniteTool.GameInterop
{
    [AddINotifyPropertyChangedInterface]
    public class GamePersistence
    {
        private readonly GameInstance instance;
        private InfiniteOffsets.Client engine => instance.Engine;
        private readonly ILogger<GamePersistence> logger;
        private InfiniteOffsets offsets;
        private ArenaAllocator? allocator;
        private bool NeedsReBootstrapped = true;
        private string[] persistenceKeys = InteropConstantData.PersistenceKeys.Keys.ToArray();
        private Dictionary<string, uint> stringToKeyMap = new();
        private IRemoteProcess process => this.instance.RemoteProcess;

        public uint CurrentParticipantId { get; private set; }

        public GamePersistence(GameInstance instance, ILogger<GamePersistence> logger)
        {
            this.instance = instance;
            this.logger = logger;

            this.instance.OnAttach += Instance_OnAttachHandler;
        }

        private void Instance_OnAttachHandler(object sender, EventArgs? args)
        {
            this.offsets = this.instance.GetCurrentOffsets();
            this.Bootstrap();
        }

        public void Bootstrap()
        {
            this.NeedsReBootstrapped = false;

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


            try
            {
                var allocator = new ArenaAllocator(this.process, 4 * 1024 * 1024); // 4MB working area

                var keyList = allocator.AllocateList<nint>(persistenceKeys.Length);
                var inList = allocator.AllocateList<nint>(persistenceKeys.Length);
                inList.AddAsciiStrings(allocator, persistenceKeys);
                inList.SyncTo();    

                this.instance.PrepareForScriptCalls();
                this.engine.Persistence_BatchTryCreateKeysFromStrings(0, keyList, inList);


                keyList.SyncFrom();
                var items = keyList.Count();

                if (items != persistenceKeys.Length)
                {
                    this.logger.LogWarning($"Mismatch of string to key results, sent {persistenceKeys.Length}, got back {items}");
                    this.NeedsReBootstrapped = true;
                }

                stringToKeyMap.Clear();
                var values = keyList.GetValues(0, (int)items);
                for (var i = 0; i < items; i++)
                {
                    var val = values[i];
                    var key = unchecked((uint)val);
                    var found = (byte)(val >> 32);

                    if (found == 1)
                    {
                        stringToKeyMap[persistenceKeys[i]] = key;
                    }
                    else
                    {
                        this.NeedsReBootstrapped = true;
                    }
                }

                allocator.Reclaim(zero: true);

                this.allocator = allocator;
                
            }
            catch
            {

            }
        }

        private void EnsureBoostrapped()
        {
            if(this.NeedsReBootstrapped)
            {
                this.Bootstrap();
            }
        }

        public uint GetPersistenceKey(string keyString)
        {
            return this.stringToKeyMap[keyString];
        }

        private static string[] EquipmentKeys = new string[]
        {
            "schematic_evade",
            "schematic_wall",
            "schematic_sensor",
            "Schematic-ShieldUpdgrade1",
            "Schematic-ShieldUpdgrade2",
            "Schematic-ShieldUpdgrade3",
            "Grapple_Upgrade_Level",
            "Evade_Upgrade_Level",
            "Wall_Upgrade_Level",
            "Sensor_Upgrade_Level",
            "Shield_Upgrade_Level",
            "spartan_griffin",
            "spartan_makovich",
            "spartan_sorel",
            "spartan_horvath",
            "spartan_vettel",
            "spartan_stone",
            "spartan_kovan",
        };

        public List<ProgressionEntry> GetEquipementRefreshPersistence()
        {
            return GetProgress(EquipmentKeys);
        }

        public class EquipmentPersistence
        {
            public bool schematic_evade { get; set; }
            public bool schematic_wall { get; set; }
            public bool schematic_sensor { get; set; }
            public bool Schematic_ShieldUpdgrade1 { get; set; }
            public bool Schematic_ShieldUpdgrade2 { get; set; }
            public bool Schematic_ShieldUpdgrade3 { get; set; }

            public byte Grapple_Upgrade_Level { get; set; }
            public byte Evade_Upgrade_Level { get; set; }
            public byte Wall_Upgrade_Level  { get; set; }
            public byte Sensor_Upgrade_Level { get; set; }
            public byte Shield_Upgrade_Level { get; set; }
        }

        public async Task RefreshEquipment()
        {
            this.instance.ShowMessage("Refreshing equipment");

            var equip = this.GetEquipementRefreshPersistence();
            var prev = new Dictionary<string, uint>();

            foreach (var e in equip)
            {
                if (e.DataType == "Boolean")
                {
                    e.GlobalValue ^= 1;
                }
                else
                {
                    prev[e.KeyName] = e.GlobalValue;
                    e.GlobalValue = 0;
                }
            }

            this.SetProgress(equip);

            await Task.Delay(10);

            foreach (var e in equip)
            {
                if (e.DataType == "Boolean")
                {
                    e.GlobalValue ^= 1;
                }
                else
                {
                    e.GlobalValue = prev[e.KeyName];
                }
            }

            this.SetProgress(equip);
        }

        public List<ProgressionEntry> GetAllProgress()
        {
            return GetProgress(persistenceKeys);
        }

        public List<ProgressionEntry> GetProgress(string[] persistenceStringKeys)
        {
            this.EnsureBoostrapped();

            if (this.allocator == null) return new List<ProgressionEntry>();

            this.instance.PrepareForScriptCalls();

            lock (this.allocator)
            {
                var keyList = allocator.AllocateList<uint>(persistenceStringKeys.Length);
                var intKeys = persistenceStringKeys.Select(k => this.stringToKeyMap[k]).ToArray();
                keyList.AddValues(intKeys);

                var globalBools = allocator.AllocateList<bit>(persistenceStringKeys.Length);
                var globalBytes = allocator.AllocateList<short>(persistenceStringKeys.Length);
                var globalLongs = allocator.AllocateList<uint>(persistenceStringKeys.Length);

                engine.Persistence_BatchGetBoolKeys(0x0, globalBools, keyList);
                engine.Persistence_BatchGetByteKeys(0x0, globalBytes, keyList);
                engine.Persistence_BatchGetLongKeys(0x0, globalLongs, keyList);

                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetBoolKeysForParticipant, 0x0, participantBools, participantId, keyList);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetByteKeysForParticipant, 0x0, participantBytes, participantId, keyList);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetLongKeysForParticipant, 0x0, participantLongs, participantId, keyList);
                
                globalBools.SyncFrom();
                globalBytes.SyncFrom();
                globalLongs.SyncFrom();

                var globalBitValues = globalBools.GetValues(0, persistenceStringKeys.Length);

                var globalByteValues = globalBytes.GetValues(0, persistenceStringKeys.Length);

                var globalLongValues = globalLongs.GetValues(0, persistenceStringKeys.Length);

                var results = new List<ProgressionEntry>(persistenceStringKeys.Length);

                var i = 0;
                foreach (var str in persistenceStringKeys)
                {
                    var type = InteropConstantData.PersistenceKeys[str];

                    uint globalValue = type switch
                    {
                        PersistenceValueType.Boolean => (uint)globalBitValues[i],
                        PersistenceValueType.Byte => (uint)globalByteValues[i],
                        PersistenceValueType.Long => globalLongValues[i],
                        _ => 0,
                    };

                    results.Add(new ProgressionEntry()
                    {
                        KeyName = str,
                        DataType = type.ToString(),
                        GlobalValue = globalValue,
                        ParticipantValue = globalValue,
                    });
                    
                    i++;
                }

                allocator.Reclaim(zero: true);
                return results;
            }
        }

        // TODO remove?
        private HashSet<string> keysToOverride = new HashSet<string>()
        {
            //"Loadout-FirstWeaponExists",
            //"Loadout-FirstWeaponTag",
            //"Loadout-FirstWeaponConfigTag"
        };

        public void SetProgress(List<ProgressionEntry> entries)
        {
            this.EnsureBoostrapped();

            var boolSet = new List<(uint key, uint globalVal, uint participantVal, bool needsOverride)>();
            var byteSet = new List<(uint key, uint globalVal, uint participantVal, bool needsOverride)>();
            var longSet = new List<(uint key, uint globalVal, uint participantVal, bool needsOverride)>();

            foreach (var entry in entries)
            {
                if(stringToKeyMap.TryGetValue(entry.KeyName, out var key)
                    && InteropConstantData.PersistenceKeys.TryGetValue(entry.KeyName, out var type))
                {
                    switch (type)
                    {
                        case PersistenceValueType.Boolean:
                            boolSet.Add((key, entry.GlobalValue, entry.ParticipantValue, keysToOverride.Contains(entry.KeyName)));
                            break;
                        case PersistenceValueType.Byte:
                            byteSet.Add((key, entry.GlobalValue, entry.ParticipantValue, keysToOverride.Contains(entry.KeyName)));
                            break;
                        case PersistenceValueType.Long:
                            longSet.Add((key, entry.GlobalValue, entry.ParticipantValue, keysToOverride.Contains(entry.KeyName)));
                            break;
                    }
                }
            }

            lock(allocator)
            {
                var boolKeys = allocator.AllocateList<uint>(boolSet.Count);
                var byteKeys = allocator.AllocateList<uint>(byteSet.Count);
                var longKeys = allocator.AllocateList<uint>(longSet.Count);
                boolKeys.AddValues(boolSet.Select(b => b.key).ToArray());
                byteKeys.AddValues(byteSet.Select(b => b.key).ToArray());
                longKeys.AddValues(longSet.Select(b => b.key).ToArray());

                var boolOverrideKeys = allocator.AllocateList<uint>(keysToOverride.Count);
                var byteOverrideKeys = allocator.AllocateList<uint>(keysToOverride.Count);
                var longOverrideKeys = allocator.AllocateList<uint>(keysToOverride.Count);
                boolOverrideKeys.AddValues(boolSet.Where(b => b.needsOverride).Select(b => b.key).ToArray());
                byteOverrideKeys.AddValues(byteSet.Where(b => b.needsOverride).Select(b => b.key).ToArray());
                longOverrideKeys.AddValues(longSet.Where(b => b.needsOverride).Select(b => b.key).ToArray());

                var globalBools = allocator.AllocateList<bit>(boolSet.Count);
                var globalBytes = allocator.AllocateList<short>(byteSet.Count);
                var globalLongs = allocator.AllocateList<uint>(longSet.Count);
                globalBools.AddValues(boolSet.Select(b => new bit(b.globalVal != 0)).ToArray());
                globalBytes.AddValues(byteSet.Select(b => (short)b.globalVal).ToArray());
                globalLongs.AddValues(longSet.Select(b => b.globalVal).ToArray());
                var globalBoolResults = allocator.AllocateList<bit>(boolSet.Count);
                var globalByteResults = allocator.AllocateList<short>(byteSet.Count);
                var globalLongResults = allocator.AllocateList<uint>(longSet.Count);

                var participantBools = allocator.AllocateList<bit>(boolSet.Count);
                var participantBytes = allocator.AllocateList<short>(byteSet.Count);
                var participantLongs = allocator.AllocateList<uint>(longSet.Count);
                participantBools.AddValues(boolSet.Select(b => new bit(b.participantVal != 0)).ToArray());
                participantBytes.AddValues(byteSet.Select(b => (short)b.participantVal).ToArray());
                participantLongs.AddValues(longSet.Select(b => b.participantVal).ToArray());
                var participantBoolResults = allocator.AllocateList<bit>(boolSet.Count);
                var participantByteResults = allocator.AllocateList<short>(byteSet.Count);
                var participantLongResults = allocator.AllocateList<uint>(longSet.Count);

                //var participantId = (nint)(CurrentParticipantId << 16);

                this.instance.PrepareForScriptCalls();

                // Clear overrides
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveBoolKeyOverrides, 0x0, DiscardList(), boolKeys);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveByteKeyOverrides, 0x0, DiscardList(), byteKeys);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveLongKeyOverrides, 0x0, DiscardList(), longKeys);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveBoolKeyOverrideForParticipant, 0x0, DiscardList(), participantId, boolKeys);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveByteKeyOverrideForParticipant, 0x0, DiscardList(), participantId, byteKeys);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveLongKeyOverrideForParticipant, 0x0, DiscardList(), participantId, longKeys);

                // Set new values

                //engine.Persistence_BatchOverrideBoolKeys(0, DiscardList(), boolOverrideKeys);
                //engine.Persistence_BatchOverrideByteKeys(0, DiscardList(), byteOverrideKeys);
                //engine.Persistence_BatchOverrideLongKeys(0, DiscardList(), longOverrideKeys);

                engine.Persistence_BatchRemoveBoolKeyOverrides(0, DiscardList(), boolKeys);
                engine.Persistence_BatchRemoveByteKeyOverrides(0, DiscardList(), byteKeys);
                engine.Persistence_BatchRemoveLongKeyOverrides(0, DiscardList(), longKeys);

                engine.Persistence_BatchSetBoolKeys(0, globalBoolResults, boolKeys, globalBools);
                engine.Persistence_BatchSetByteKeys(0, globalByteResults, byteKeys, globalBytes);
                engine.Persistence_BatchSetLongKeys(0, globalLongResults, longKeys, globalLongs);


                allocator.Reclaim(zero: true);
            }

            nint DiscardList()
            {
                // Allocate enough room for list header to get results back in
                // Body isn't required as outputs seem to be allocated by the engine?
                return allocator.Allocate(BlamEngineList<nint>.GetRequiredSize(0).header);
            }
        }

        [AddINotifyPropertyChangedInterface]
        public class ProgressionEntry
        {
            public string KeyName { get; set; }

            public string DataType { get; set; }

            public uint GlobalValue { get; set; }

            public uint ParticipantValue { get; set; }

            public string GlobalValueString => GlobalValue.ToString("X16");
            public string ParticipantValueString => GlobalValue.ToString("X16");
        }
    }
}
