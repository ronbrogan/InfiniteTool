﻿using InfiniteTool.GameInterop.EngineDataTypes;
using Microsoft.Extensions.Logging;
using Superintendent.Core.Remote;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace InfiniteTool.GameInterop
{
    public class GamePersistence : INotifyPropertyChanged
    {
        private readonly GameInstance instance;
        private readonly InfiniteOffsets offsets;
        private readonly ILogger<GamePersistence> logger;
        private ArenaAllocator? allocator;
        private string[] persistenceKeys = InteropConstantData.PersistenceKeys.Keys.ToArray();
        private Dictionary<string, uint> stringToKeyMap = new();
        private IRemoteProcess process => this.instance.RemoteProcess;

        public event PropertyChangedEventHandler? PropertyChanged;

        public uint CurrentParticipantId { get; private set; }

        public GamePersistence(GameInstance instance, InfiniteOffsets offsets, ILogger<GamePersistence> logger)
        {
            this.instance = instance;
            this.offsets = offsets;
            this.logger = logger;

            this.instance.OnAttach += Instance_OnAttachHandler;
        }

        private void Instance_OnAttachHandler(object sender, EventArgs? args)
        {
            this.Bootstrap();
        }

        public void Bootstrap()
        {
            if (this.allocator != null)
            {
                try
                {
                    lock(this.allocator)
                    {
                        this.allocator.Dispose();
                    }
                }
                catch { }
            }

            var allocator = new ArenaAllocator(this.process, 4 * 1024 * 1024); // 4MB working area

            var keyList = allocator.AllocateList<nint>(persistenceKeys.Length);
            var inList = allocator.AllocateList<nint>(persistenceKeys.Length);

            foreach (var keyString in persistenceKeys)
            {
                inList.AddAsciiString(allocator, keyString);
            }

            this.process.CallFunction<nint>(
                this.offsets.Persistence_KeysFromStrings_Batch, 
                0x0, 
                keyList.Address, 
                inList.Address);

            keyList.SyncFrom();

            var items = keyList.Count();

            if(items != persistenceKeys.Length)
            {
                this.logger.LogWarning($"Mismatch of string to key results, sent {persistenceKeys.Length}, got back {items}");
            }

            for (var i = 0; i < items; i++)
            {
                var val = keyList.GetValue(i);
                var key = unchecked((uint)val);
                var found = (byte)(val >> 32);

                if(found == 1)
                {
                    stringToKeyMap[persistenceKeys[i]] = key;
                }
                else
                {
                    this.logger.LogWarning($"Miss on string to key, sent '{persistenceKeys[i]}', got back {val:x16}");
                }
            }

            allocator.Reclaim(zero: true);

            this.allocator = allocator;
        }

        public List<Entry> GetAllProgress()
        {
            if (this.allocator == null) return new List<Entry>();

            var keys = this.stringToKeyMap.ToArray();

            this.PrepareForPersistenceCalls();

            lock (this.allocator)
            {
                var keyList = allocator.AllocateList<uint>(persistenceKeys.Length);

                foreach (var (str, key) in keys)
                {
                    keyList.AddValue(key);
                }

                var globalBools = allocator.AllocateList<bit>(persistenceKeys.Length);
                var globalBytes = allocator.AllocateList<short>(persistenceKeys.Length);
                var globalLongs = allocator.AllocateList<uint>(persistenceKeys.Length);

                var participantBools = allocator.AllocateList<bit>(persistenceKeys.Length);
                var participantBytes = allocator.AllocateList<short>(persistenceKeys.Length);
                var participantLongs = allocator.AllocateList<uint>(persistenceKeys.Length);

                var participantId = (nint)(CurrentParticipantId << 16);

                process.CallFunction<nint>(this.offsets.Persistence_GetBools_Batch, 0x0, globalBools.Address, keyList.Address);
                process.CallFunction<nint>(this.offsets.Persistence_GetBytes_Batch, 0x0, globalBytes.Address, keyList.Address);
                process.CallFunction<nint>(this.offsets.Persistence_GetLongs_Batch, 0x0, globalLongs.Address, keyList.Address);

                process.CallFunction<nint>(this.offsets.Persistence_GetBoolsForParticipant_Batch, 0x0, participantBools.Address, participantId, keyList.Address);
                process.CallFunction<nint>(this.offsets.Persistence_GetBytesForParticipant_Batch, 0x0, participantBytes.Address, participantId, keyList.Address);
                process.CallFunction<nint>(this.offsets.Persistence_GetLongsForParticipant_Batch, 0x0, participantLongs.Address, participantId, keyList.Address);
                
                globalBools.SyncFrom();
                globalBytes.SyncFrom();
                globalLongs.SyncFrom();
                
                participantBools.SyncFrom();
                participantBytes.SyncFrom();
                participantLongs.SyncFrom();

                var keyLookup = new Dictionary<string, uint>();

                var results = new List<Entry>(keys.Length);

                var i = 0;
                foreach (var (str, key) in keys)
                {
                    var type = InteropConstantData.PersistenceKeys[str];

                    uint globalValue = type switch
                    {
                        PersistenceValueType.Boolean => (uint)globalBools.GetValue(i),
                        PersistenceValueType.Byte => (uint)globalBytes.GetValue(i),
                        PersistenceValueType.Long => globalLongs.GetValue(i),
                        _ => 0,
                    };

                    uint participantValue = type switch
                    {
                        PersistenceValueType.Boolean => (uint)participantBools.GetValue(i),
                        PersistenceValueType.Byte => (uint)participantBytes.GetValue(i),
                        PersistenceValueType.Long => participantLongs.GetValue(i),
                        _ => 0,
                    };

                    results.Add(new Entry()
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

        private void PrepareForPersistenceCalls()
        {
            process.Read(this.offsets.PersistenceData_TlsIndexOffset, out int persistenceIndex);
            process.SetTlsValue(persistenceIndex, instance.ReadMainTebPointer(persistenceIndex));

            process.Read(this.offsets.PersistenceUnknown_TlsIndexOffset, out int unknownIndexA);
            process.SetTlsValue(unknownIndexA, instance.ReadMainTebPointer(unknownIndexA));

            process.Read(this.offsets.PlayerDatum_TlsIndexOffset, out int playerDatumIndex);
            process.SetTlsValue(playerDatumIndex, instance.ReadMainTebPointer(playerDatumIndex));

            process.Read(this.offsets.ParticipantDatum_TlsIndexOffset, out int participantDatumIndex);
            var participantDatumLocation = instance.ReadMainTebPointer(participantDatumIndex);
            process.SetTlsValue(participantDatumIndex, participantDatumLocation);

            process.ReadAt(participantDatumLocation + 0x78, out nint participantAddress);
            process.ReadAt(participantAddress, out uint participantId);

            this.CurrentParticipantId = participantId;
        }

        public class Entry : INotifyPropertyChanged
        {
            public string KeyName { get; set; }

            public string DataType { get; set; }

            public uint GlobalValue { get; set; }

            public uint ParticipantValue { get; set; }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}