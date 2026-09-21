#if UNITY_EDITOR
using System.Collections.Generic;
using SiegeCore.Rat;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RatLoadoutDefinition))]
public sealed class RatLoadoutDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        RatLoadoutDefinition definition = (RatLoadoutDefinition)target;
        List<RatLoadoutEntry> validEntries = new List<RatLoadoutEntry>();
        int invalidCount = definition.CopyValidEntries(validEntries);
        int cardCount = 0;
        foreach (RatLoadoutEntry entry in validEntries)
        {
            cardCount += entry.Copies;
        }

        if (invalidCount > 0)
        {
            EditorGUILayout.HelpBox(
                invalidCount + " invalid loadout entr" + (invalidCount == 1 ? "y" : "ies")
                + " will be ignored. Use unique Rank1 RatDefinitions with Copies >= 1.",
                MessageType.Warning);
        }

        if (validEntries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No valid cards. RatDispenser will use the Basic Rank1 fallback when available.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Valid cards: " + validEntries.Count + " entries / " + cardCount + " cards",
                MessageType.Info);
        }
    }
}
#endif
