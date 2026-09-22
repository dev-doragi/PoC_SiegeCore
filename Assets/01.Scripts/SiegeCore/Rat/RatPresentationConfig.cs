using UnityEngine;

namespace SiegeCore.Rat
{
    [CreateAssetMenu(menuName = "SiegeCore/Rat Presentation Config")]
    public sealed class RatPresentationConfig : ScriptableObject
    {
        [Header("Faction")]
        public Color AllyColor = Color.white;
        public Color EnemyColor = new Color(1f, 0.55f, 0.55f, 1f);

        [Header("Moving")]
        [Min(0f)] public float WiggleAmount = 0.09f;
        [Min(0f)] public float WiggleSpeed = 20f;
        [Min(0f)] public float BounceAmount = 0.08f;
        [Min(0f)] public float BounceSpeed = 16f;
        [Min(0f)] public float StretchAmount = 0.08f;

        [Header("Idle")]
        [Min(0f)] public float IdleBreathAmount = 0.025f;
        [Min(0f)] public float IdleBreathSpeed = 3.2f;

        [Header("Death")]
        [Min(0f)] public float DeathSinkDistance = 0.3f;
        [Min(0f)] public float DeathDuration = 0.6f;

        [Header("Shadow")]
        public Vector2 ShadowScale = new Vector2(1f, 0.3f);
        [Range(0f, 1f)] public float ShadowAlpha = 0.35f;
        [Range(0.1f, 1f)] public float ShadowAirScale = 0.6f;
        public Vector2 ShadowOffset = new Vector2(0f, -0.8f);

        [Header("Impact")]
        [Range(0.1f, 1f)] public float SquashRatio = 0.65f;
    }
}
