#if UNITY_EDITOR
using SiegeCore.Rat;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RatCatalog))]
public sealed class RatCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        RatCatalog catalog = (RatCatalog)target;
        int invalidCount = 0;
        if (catalog.Entries != null)
        {
            foreach (RatCatalogEntry entry in catalog.Entries)
            {
                RatAgent prefab = entry.Pool != null && entry.Pool.Prefab != null
                    ? entry.Pool.Prefab.GetComponent<RatAgent>() : null;
                if (entry.Definition == null
                    || prefab == null
                    || prefab.Definition != entry.Definition)
                {
                    invalidCount++;
                }
            }
        }

        if (!catalog.HasValidFallback)
        {
            EditorGUILayout.HelpBox(
                "Fallback must be the Basic Rank1 RatDefinition.",
                MessageType.Error);
        }

        if (invalidCount > 0)
        {
            EditorGUILayout.HelpBox(
                invalidCount + " catalog entr" + (invalidCount == 1 ? "y" : "ies")
                + " has a missing or mismatched PoolDefinition prefab.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("All catalog entries resolve to matching Rat prefabs.", MessageType.Info);
        }
    }
}
#endif
