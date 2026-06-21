using System;
using System.IO;
using UnityEngine;

namespace DistilleryDiscovery
{
    public interface IStateStorage
    {
        bool Exists { get; }
        void Write(string json);
        string Read();
        void Delete();
    }

    public sealed class FileStateStorage : IStateStorage
    {
        private readonly string path;
        public FileStateStorage(string path) => this.path = path;
        public bool Exists => File.Exists(path);
        public void Write(string json) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, json); }
        public string Read() => File.ReadAllText(path);
        public void Delete() { if (File.Exists(path)) File.Delete(path); }
    }

    public sealed class SaveService
    {
        private readonly IStateStorage storage;
        public SaveService(IStateStorage storage) => this.storage = storage;
        public bool HasSave => storage.Exists;
        public void Save(PlayerState state)
        {
            state.lastSavedAtUtc = DateTime.UtcNow.ToString("O");
            storage.Write(JsonUtility.ToJson(state, true));
        }
        public PlayerState Load()
        {
            if (!storage.Exists) throw new InvalidOperationException("No local save exists.");
            var state = JsonUtility.FromJson<PlayerState>(storage.Read());
            return state ?? throw new InvalidOperationException("Local save is invalid.");
        }
        public void Reset() => storage.Delete();
    }
}
