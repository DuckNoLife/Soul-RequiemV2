#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.UI;

namespace SoulRequiem.EditorTools
{
    /// <summary>Soul Requiem ▸ Build Map ▸ Title Screen (main menu): creates Assets/Scenes/Title.unity and puts it FIRST in Build Settings.</summary>
    public static class TitleSceneBuilder
    {
        private const string Path = "Assets/Scenes/Title.unity";

        public static void Build()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Soul Requiem", "Stop Play first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.015f, 0.05f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var title = new GameObject("Title");
            title.AddComponent<TitleScreen>();

            EditorSceneManager.SaveScene(scene, Path);

            var list = EditorBuildSettings.scenes.Where(s => s.path != Path).ToList();
            list.Insert(0, new EditorBuildSettingsScene(Path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Soul Requiem] Built " + Path + " and put it first in Build Settings. Press Play to try the main menu.");
        }
    }
}
#endif
