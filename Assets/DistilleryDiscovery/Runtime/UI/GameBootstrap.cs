using System.IO;
using UnityEngine;

namespace DistilleryDiscovery
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (Object.FindFirstObjectByType<PrototypeUI>() != null) return;
            var config = ConfigLoader.LoadFromResources();
            var save = new SaveService(new FileStateStorage(Path.Combine(Application.persistentDataPath, "distillery_save.json")));
            var state = save.HasSave ? save.Load() : GameService.NewState(config);
            var host = new GameObject("Distillery Discovery");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<PrototypeUI>().Initialize(new GameService(config, state), save);
        }
    }
}

