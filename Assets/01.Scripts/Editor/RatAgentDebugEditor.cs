using UnityEditor;
using UnityEngine;
using SiegeCore.Rat;

[CustomEditor(typeof(RatAgent))]
public sealed class RatAgentDebugEditor : Editor
{
    private bool _showHistory = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RatAgent agent = (RatAgent)target;
        RatGroundAI groundAI = agent.GetComponent<RatGroundAI>();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("FSM Debug", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("Current State", agent.State);
            EditorGUILayout.EnumPopup("Condition", agent.Condition);
            EditorGUILayout.EnumPopup("Ground Mode", agent.GroundMode);
            EditorGUILayout.FloatField("Health", agent.Health);

            if (groundAI != null)
            {
                EditorGUILayout.EnumPopup("Assignment", groundAI.Assignment);
                EditorGUILayout.Toggle("Is Moving", groundAI.IsMoving);
                EditorGUILayout.Vector2Field("Move Direction", groundAI.MoveDirection);
            }
        }

        _showHistory = EditorGUILayout.Foldout(
            _showHistory,
            "Recent State Transitions (latest 16)",
            true);

        if (_showHistory)
        {
            if (agent.StateHistory.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No state transition has been recorded yet.",
                    MessageType.Info);
            }
            else
            {
                for (int index = agent.StateHistory.Count - 1; index >= 0; index--)
                {
                    RatAgent.StateTransition transition = agent.StateHistory[index];
                    string label = string.Format(
                        "#{0}  {1:0.00}s  {2} -> {3}",
                        transition.Frame,
                        transition.Time,
                        transition.PreviousState,
                        transition.NextState);
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                }
            }
        }

        if (Application.isPlaying && GUILayout.Button("Clear FSM History"))
        {
            agent.ClearStateHistory();
            Repaint();
        }

        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
