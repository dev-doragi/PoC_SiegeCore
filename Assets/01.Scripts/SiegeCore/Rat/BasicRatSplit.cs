using SiegeCore.Cannon;
using UnityEngine;

namespace SiegeCore.Rat
{
    internal static class BasicRatSplit
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
                Vector3 landingPosition = onGround
                    ? battlefield.NearestFloor(spawnPosition, faction)
                    : battlefield.FloorBelow(spawnPosition, faction);
                RatAgent rat = factory.Spawn(definition, faction, landingPosition, true, true);
                if (rat == null) continue;
                RatGroundAI groundAI = rat.GetComponent<RatGroundAI>();
                if (infiltrated && groundAI != null) groundAI.BeginBaseInfiltration();
                float fallHeight = Mathf.Max(0.1f, spawnPosition.y - landingPosition.y);
                rat.Carryable.BeginRatFall(battlefield.Ground, fallHeight);
            }
        }
    }
}
