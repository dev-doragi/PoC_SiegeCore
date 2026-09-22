using UnityEngine;

namespace SiegeCore.Rat
{
    public sealed class RatBatFlightMotion
    {
        private enum FlightPhase
        {
            None,
            NormalKnockback,
            FullChargeRoute,
            WallReturn
        }

        private FlightPhase _phase;

        public int SwingId { get; private set; }
        public bool CanCollisionFuse { get; private set; }
        public float FusionMinimumSpeed { get; private set; }
        public Transform ReturnTarget { get; private set; }
        public bool IsFullChargeRoute { get { return _phase == FlightPhase.FullChargeRoute; } }
        public bool IsWallReturn { get { return _phase == FlightPhase.WallReturn; } }

        public void Begin(
            int swingId,
            bool fullCharge,
            float fusionMinimumSpeed,
            Transform returnTarget)
        {
            SwingId = swingId;
            CanCollisionFuse = fullCharge;
            FusionMinimumSpeed = Mathf.Max(0f, fusionMinimumSpeed);
            ReturnTarget = returnTarget;

            if (fullCharge)
            {
                _phase = FlightPhase.FullChargeRoute;
            }
            else
            {
                _phase = FlightPhase.NormalKnockback;
            }
        }

        public void BeginFusion(
            int swingId,
            float fusionMinimumSpeed,
            Transform returnTarget)
        {
            Begin(swingId, true, fusionMinimumSpeed, returnTarget);
        }

        public void BeginWallReturn()
        {
            _phase = FlightPhase.WallReturn;
        }

        public void Reset()
        {
            SwingId = 0;
            _phase = FlightPhase.None;
            CanCollisionFuse = false;
            FusionMinimumSpeed = 0f;
            ReturnTarget = null;
        }
    }
}
