using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DistilleryDiscovery.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Distillery Discovery/Configure MVP Project")]
        public static void CreateMainScene()
        {
            const string sceneDirectory = "Assets/Scenes";
            const string scenePath = sceneDirectory + "/Main.unity";
            if (!Directory.Exists(sceneDirectory)) Directory.CreateDirectory(sceneDirectory);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Main";
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.08f, 0.12f);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            PlayerSettings.companyName = "Idle Casino Lab";
            PlayerSettings.productName = "Distillery Discovery MVP";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.idlecasinolab.distillerydiscovery");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            AssetDatabase.SaveAssets();
            Debug.Log("Distillery Discovery project configured; Main scene added to build settings.");
        }
    }
}
