using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Load Scene Command", menuName = DeveloperConsole.CommandBasePath + "Load Scene Command")]
    public class LoadSceneCommand : ConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Usage: LoadScene <sceneName|scenePath|buildIndex>.\nLoads the specified scene from build settings.";

        public override string HelpText => m_HelpText;

        public override string[] Presets
        {
            get
            {
                List<string> presets = new(base.Presets ?? Array.Empty<string>());
                string commandName = Name;
                if (string.IsNullOrWhiteSpace(commandName))
                    return presets.ToArray();

                string[] scenePaths = GetBuildScenePaths();
                if (scenePaths.Length == 0)
                    return presets.ToArray();

                Dictionary<string, int> sceneNameCounts = new(StringComparer.OrdinalIgnoreCase);
                foreach (string scenePath in scenePaths)
                {
                    string sceneName = GetSceneName(scenePath);
                    if (string.IsNullOrEmpty(sceneName))
                        continue;

                    sceneNameCounts.TryGetValue(sceneName, out int count);
                    sceneNameCounts[sceneName] = count + 1;
                }

                foreach (string scenePath in scenePaths)
                {
                    string sceneName = GetSceneName(scenePath);
                    if (string.IsNullOrEmpty(sceneName))
                        continue;

                    string presetArgument = sceneNameCounts[sceneName] > 1 ? scenePath : sceneName;
                    presets.Add($"{commandName} {FormatPresetArgument(presetArgument)}");
                }

                return presets.ToArray();
            }
        }

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            string requestedScene = string.Join(" ", args).Trim();
            if (!TryResolveScene(requestedScene, out string scenePath))
                return false;


            Debug.Log($"Loading scene '{scenePath}'.");
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            return true;
        }

        private string[] GetBuildScenePaths()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            if (sceneCount <= 0)
                return Array.Empty<string>();

            List<string> scenePaths = new(sceneCount);
            for (int i = 0; i < sceneCount; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (!string.IsNullOrWhiteSpace(scenePath))
                    scenePaths.Add(scenePath);
            }

            return scenePaths.ToArray();
        }

        private string GetSceneName(string scenePath)
        {
            return Path.GetFileNameWithoutExtension(scenePath);
        }

        private string FormatPresetArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return string.Empty;

            return argument.IndexOf(' ') >= 0 ? $"\"{argument}\"" : argument;
        }

        private bool TryResolveScene(string requestedScene, out string resolvedScenePath)
        {
            resolvedScenePath = null;

            if (string.IsNullOrWhiteSpace(requestedScene))
            {
                Debug.LogWarning("Scene name cannot be empty.");
                return false;
            }

            string trimmedScene = requestedScene.Trim();

            if (int.TryParse(trimmedScene, out int buildIndex))
            {
                if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogWarning($"Scene build index '{buildIndex}' is out of range.");
                    return false;
                }

                resolvedScenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                return !string.IsNullOrWhiteSpace(resolvedScenePath);
            }

            string normalizedRequestedScene = trimmedScene.Replace('\\', '/');

            foreach (string scenePath in GetBuildScenePaths())
            {
                string normalizedScenePath = scenePath.Replace('\\', '/');
                if (normalizedScenePath.Equals(normalizedRequestedScene, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedScenePath = scenePath;
                    return true;
                }

                string sceneName = GetSceneName(scenePath);
                if (!sceneName.Equals(normalizedRequestedScene, StringComparison.OrdinalIgnoreCase))
                    continue;

                resolvedScenePath = scenePath;
                return true;
            }


            Debug.LogWarning($"Scene '{trimmedScene}' was not found in build settings.");
            return false;
        }
    }
}
