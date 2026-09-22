using SiegeCore.Rat;

namespace SiegeCore.Cannon
{
    public sealed class CannonMagazineEntry
    {
        public CannonMagazineEntry(RatAgent rat)
        {
            Rat = rat;
            ReadyTime = float.PositiveInfinity;
        }

        public RatAgent Rat { get; private set; }
        public bool IsReady { get; private set; }
        public float ReadyTime { get; private set; }

        public void MarkReady(float readyTime)
        {
            IsReady = true;
            ReadyTime = readyTime;
        }
    }
}
