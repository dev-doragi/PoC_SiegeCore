using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    public sealed class GroundBlockTarget : MonoBehaviour
    {
        private RatAgent _agent;

        public RatAgent Agent
        {
            get { return _agent; }
        }

        public int Required
        {
            get
            {
                if (_agent == null || _agent.Definition == null)
                {
                    return 1;
                }

                return Mathf.Max(1, _agent.Definition.Ground.BlockRequired);
            }
        }

        public GroundBlocker Blocker { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
        }

        private void OnDisable()
        {
            ReleaseBlocker();
        }

        public void ReleaseBlocker()
        {
            GroundBlocker blocker = Blocker;
            if (blocker != null)
            {
                blocker.Release(this);
                return;
            }

            Blocker = null;
        }

        public bool AssignBlocker(GroundBlocker blocker)
        {
            if (blocker == null)
            {
                return false;
            }

            if (Blocker != null && Blocker != blocker)
            {
                return false;
            }

            Blocker = blocker;
            return true;
        }

        public void ClearBlocker(GroundBlocker blocker)
        {
            if (Blocker == blocker)
            {
                Blocker = null;
            }
        }
    }
}
