using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VisualAssetAudit
{
    [MenuItem("Game/Audit Visual Assets")]
    public static void Audit()
    {
        var missingEnemyPortraits = FindAssets<EnemyTypeData>()
            .Where(asset => !asset.portraitSprite)
            .Select(AssetDatabase.GetAssetPath)
            .OrderBy(path => path)
            .ToArray();
        var missingArtifactIcons = FindAssets<RelicData>()
            .Where(asset => !asset.iconSprite)
            .Select(AssetDatabase.GetAssetPath)
            .OrderBy(path => path)
            .ToArray();

        Debug.Log($"Visual asset audit: {missingEnemyPortraits.Length} enemy portraits and {missingArtifactIcons.Length} artifact icons are unassigned. " +
                  "Existing glyph/placeholder fallbacks remain active.\n" +
                  "Enemy portraits:\n" + string.Join("\n", missingEnemyPortraits) +
                  "\nArtifact icons:\n" + string.Join("\n", missingArtifactIcons));
    }

    static IEnumerable<T> FindAssets<T>() where T : Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset);
    }
}
