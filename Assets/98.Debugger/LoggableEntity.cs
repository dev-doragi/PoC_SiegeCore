using System;
using UnityEngine;

public class LoggableEntity : MonoBehaviour
{
    [SerializeField] private string entityId;
    [SerializeField] private string displayName;
    [SerializeField] private string team;
    [SerializeField] private string entityType;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(entityId))
            entityId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;

        if (string.IsNullOrWhiteSpace(team))
            team = "Neutral";

        if (string.IsNullOrWhiteSpace(entityType))
            entityType = "Unknown";
    }

    public string EntityId => entityId;
    public string DisplayName => displayName;
    public string Team => team;
    public string EntityType => entityType;
}
