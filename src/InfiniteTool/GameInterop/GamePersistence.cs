using InfiniteTool.GameInterop.EngineDataTypes;
using InfiniteTool.GameInterop.Internal;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Superintendent.Core.Remote;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace InfiniteTool.GameInterop
{
    [AddINotifyPropertyChangedInterface]
    public class GamePersistence
    {
        private readonly GameInstance instance;
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

                this.process.CallFunction<nint>(
                    this.offsets.Persistence_BatchTryCreateKeysFromStrings,
                    0x0,
                    keyList.Address,
                    inList.Address);

                keyList.SyncFrom();

                var items = keyList.Count();

                if (items != persistenceKeys.Length)
                {
                    this.logger.LogWarning($"Mismatch of string to key results, sent {persistenceKeys.Length}, got back {items}");
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
                        this.logger.LogWarning($"Miss on string to key, sent '{persistenceKeys[i]}', got back {val:x16}");
                        this.NeedsReBootstrapped = true;
                    }
                }

                allocator.Reclaim(zero: true);

                this.allocator = allocator;
                this.NeedsReBootstrapped = false;
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

        public List<ProgressionEntry> GetAllProgress()
        {
            this.EnsureBoostrapped();

            if (this.allocator == null) return new List<ProgressionEntry>();

            var keys = this.stringToKeyMap.ToArray();

            this.instance.PrepareForScriptCalls();

            lock (this.allocator)
            {
                var keyList = allocator.AllocateList<uint>(persistenceKeys.Length);
                var intKeys = keys.Select(k => k.Value).ToArray();
                keyList.AddValues(intKeys);

                var globalBools = allocator.AllocateList<bit>(persistenceKeys.Length);
                var globalBytes = allocator.AllocateList<short>(persistenceKeys.Length);
                var globalLongs = allocator.AllocateList<uint>(persistenceKeys.Length);

                var participantBools = allocator.AllocateList<bit>(persistenceKeys.Length);
                var participantBytes = allocator.AllocateList<short>(persistenceKeys.Length);
                var participantLongs = allocator.AllocateList<uint>(persistenceKeys.Length);

                //var participantId = (nint)(CurrentParticipantId << 16);

                process.CallFunction<nint>(this.offsets.Persistence_BatchGetBoolKeys, 0x0, globalBools, keyList);
                process.CallFunction<nint>(this.offsets.Persistence_BatchGetByteKeys, 0x0, globalBytes, keyList);
                process.CallFunction<nint>(this.offsets.Persistence_BatchGetLongKeys, 0x0, globalLongs, keyList);

                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetBoolKeysForParticipant, 0x0, participantBools, participantId, keyList);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetByteKeysForParticipant, 0x0, participantBytes, participantId, keyList);
                //process.CallFunction<nint>(this.offsets.Persistence_BatchGetLongKeysForParticipant, 0x0, participantLongs, participantId, keyList);
                
                globalBools.SyncFrom();
                globalBytes.SyncFrom();
                globalLongs.SyncFrom();
                
                participantBools.SyncFrom();
                participantBytes.SyncFrom();
                participantLongs.SyncFrom();

                var globalBitValues = globalBools.GetValues(0, keys.Length);
                var participantBitValues = participantBools.GetValues(0, keys.Length);

                var globalByteValues = globalBytes.GetValues(0, keys.Length);
                var participantByteValues = participantBytes.GetValues(0, keys.Length);

                var globalLongValues = globalLongs.GetValues(0, keys.Length);
                var participantLongValues = participantLongs.GetValues(0, keys.Length);

                var results = new List<ProgressionEntry>(keys.Length);

                var i = 0;
                foreach (var (str, key) in keys)
                {
                    var type = InteropConstantData.PersistenceKeys[str];

                    uint globalValue = type switch
                    {
                        PersistenceValueType.Boolean => (uint)globalBitValues[i],
                        PersistenceValueType.Byte => (uint)globalByteValues[i],
                        PersistenceValueType.Long => globalLongValues[i],
                        _ => 0,
                    };

                    uint participantValue = type switch
                    {
                        PersistenceValueType.Boolean => (uint)participantBitValues[i],
                        PersistenceValueType.Byte => (uint)participantByteValues[i],
                        PersistenceValueType.Long => participantLongValues[i],
                        _ => 0,
                    };

                    results.Add(new ProgressionEntry()
                    {
                        KeyName = str,
                        DataType = type.ToString(),
                        GlobalValue = globalValue,
                        ParticipantValue = participantValue,
                    });
                    
                    i++;
                }

                allocator.Reclaim(zero: true);
                return results;
            }
        }

        public void SetProgress(List<ProgressionEntry> entries)
        {
            this.EnsureBoostrapped();

            var boolSet = new List<(uint key, uint globalVal, uint participantVal)>();
            var byteSet = new List<(uint key, uint globalVal, uint participantVal)>();
            var longSet = new List<(uint key, uint globalVal, uint participantVal)>();

            foreach (var entry in entries)
            {
                if(stringToKeyMap.TryGetValue(entry.KeyName, out var key)
                    && InteropConstantData.PersistenceKeys.TryGetValue(entry.KeyName, out var type))
                {
                    switch (type)
                    {
                        case PersistenceValueType.Boolean:
                            boolSet.Add((key, entry.GlobalValue, entry.ParticipantValue));
                            break;
                        case PersistenceValueType.Byte:
                            byteSet.Add((key, entry.GlobalValue, entry.ParticipantValue));
                            break;
                        case PersistenceValueType.Long:
                            longSet.Add((key, entry.GlobalValue, entry.ParticipantValue));
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

                var participantId = (nint)(CurrentParticipantId << 16);

                this.instance.PrepareForScriptCalls();

                // Clear overrides
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveBoolKeyOverrides, 0x0, DiscardList(), boolKeys);
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveByteKeyOverrides, 0x0, DiscardList(), byteKeys);
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveLongKeyOverrides, 0x0, DiscardList(), longKeys);
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveBoolKeyOverrideForParticipant, 0x0, DiscardList(), participantId, boolKeys);
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveByteKeyOverrideForParticipant, 0x0, DiscardList(), participantId, byteKeys);
                process.CallFunction<nint>(this.offsets.Persistence_BatchRemoveLongKeyOverrideForParticipant, 0x0, DiscardList(), participantId, longKeys);

                // Set new values
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetBoolKeys, 0x0, globalBoolResults, boolKeys, globalBools);
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetByteKeys, 0x0, globalByteResults, byteKeys, globalBytes);
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetLongKeys, 0x0, globalLongResults, longKeys, globalLongs);
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetBoolKeysForParticipant, 0x0, participantBoolResults, participantId, boolKeys, participantBools);
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetByteKeysForParticipant, 0x0, participantByteResults, participantId, byteKeys, participantBytes);
                process.CallFunction<nint>(this.offsets.Persistence_BatchSetLongKeysForParticipant, 0x0, participantLongResults, participantId, longKeys, participantLongs);

                //process.CallFunction<nint>(this.offsets.Player_SaveLoadoutToPersistentStorage, participantId);

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
        }
    }
}
