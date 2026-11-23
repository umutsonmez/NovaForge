using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace NovaForge.Unity.EditorTests
{
    /// <summary>
    /// Unity can only serialize a MonoBehaviour / ScriptableObject reference (scenes, prefabs, assets) when the class
    /// lives in a file with the same name. Otherwise saved scenes show "The referenced script is missing".
    /// Runtime AddComponent still works, so only this check catches it early.
    /// </summary>
    public class ScriptFileNameTests
    {
        [Test]
        public void EverySerializableUnityType_HasAMatchingScriptFile()
        {
            var withScript = new HashSet<Type>(
                AssetDatabase.FindAssets("t:MonoScript", new[] { "Packages" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(s => s != null)
                    .Select(s => s.GetClass())
                    .Where(t => t != null));

            var missing = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("NovaForge.") && !a.GetName().Name.Contains("Test"))
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsGenericType &&
                            (typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(t) || typeof(UnityEngine.ScriptableObject).IsAssignableFrom(t)))
                .Where(t => !withScript.Contains(t))
                .Select(t => $"{t.Assembly.GetName().Name}: {t.FullName}")
                .ToList();

            CollectionAssert.IsEmpty(missing, "Move each of these into <ClassName>.cs:\n" + string.Join("\n", missing));
        }
    }
}
