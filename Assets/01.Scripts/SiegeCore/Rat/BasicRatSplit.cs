using SiegeCore.Cannon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    public static class BasicRatSplit
    {
        public static void Spawn(RatDefinition definition, RatFactory factory, VehicleSide faction,
            Vector3 position, bool onGround, bool infiltrated)
        {
            if (factory == null || !factory.IsReady || definition == null) return;
            RatBattlefield battlefield = factory.Battlefield;
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 0.2f;
                Vector3 spawnPosition = position + new Vector3(offset.x, offset.y, 0f);

                Vector3 floorPosition;
                if (!battlefield.TryGetBattlefieldLandingPosition(
                    spawnPosition.x,
                    out floorPosition))
                {
                    RatAgent fallingRat = factory.Spawn(
                        definition,
                        faction,
                        floorPosition,
                        true,
                        false);
                    if (fallingRat == null) continue;

                    float discardFallHeight =
                        Mathf.Max(0.1f, spawnPosition.y - floorPosition.y);
                    fallingRat.BeginFallAndRelease(discardFallHeight);
                    continue;
                }

                if (!battlefield.TryFindSplitLandingPosition(
                    spawnPosition,
                    faction,
                    onGround,
                    infiltrated,
                    out Vector3 landingPosition,
                    out Tilemap landingGround))
                {
                    Debug.LogWarning(
                        "[BasicRatSplit] No valid split landing position was found.",
                        factory);
                    continue;
                }

                RatAgent rat = factory.Spawn(definition, faction, landingPosition, true, true);
                if (rat == null) continue;
                RatGroundBehaviour groundAI = rat.GroundBehaviour;
                if (infiltrated && groundAI != null) groundAI.BeginBaseInfiltration();
                float fallHeight = Mathf.Max(0.1f, spawnPosition.y - landingPosition.y);
                rat.BeginFall(fallHeight, landingGround);
            }
        }
    }
}
