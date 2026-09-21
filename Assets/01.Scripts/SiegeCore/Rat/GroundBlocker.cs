using System.Collections.Generic;
using UnityEngine;

namespace SiegeCore.Rat
{
    [RequireComponent(typeof(RatAgent))]
    public sealed class GroundBlocker : MonoBehaviour
    {
        private RatAgent _agent;
        private readonly List<GroundBlockTarget> _targets =
            new List<GroundBlockTarget>();

        public RatAgent Agent
        {
            get { return _agent; }
        }

        public int Capacity
        {
            get
            {
                if (_agent == null || _agent.Definition == null)
                {
                    return 0;
                }

                return Mathf.Max(0, _agent.Definition.Ground.BlockCapacity);
            }
        }

        public int Occupied { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<RatAgent>();
        }

        private void OnEnable()
        {
            if (_agent != null)
            {
                _agent.StateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_agent != null)
            {
                _agent.StateChanged -= HandleStateChanged;
            }

            ReleaseAll();
        }

        private void Update()
        {
            RemoveInvalidTargets();
        }

        public bool CanBlock(GroundBlockTarget target)
        {
            if (target == null
                || target.Agent == null
                || _agent == null
                || _agent.IsDead
                || _agent.State != RatState.GroundCombat
                || target.Agent.IsDead
                || target.Agent.Faction == _agent.Faction)
            {
                return false;
            }

            if (target.Blocker == this)
            {
                return true;
            }

            if (target.Blocker != null)
            {
                return false;
            }

            return Occupied + target.Required <= Capacity;
        }

        public bool TryBlock(GroundBlockTarget target)
        {
            if (target == null)
            {
                return false;
            }

            if (target.Blocker == this)
            {
                return true;
            }

            if (!CanBlock(target) || !target.AssignBlocker(this))
            {
                return false;
            }

            _targets.Add(target);
            RecalculateOccupied();
            return true;
        }

        public void Release(GroundBlockTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (_targets.Remove(target))
            {
                target.ClearBlocker(this);
                RecalculateOccupied();
            }
        }

        public void ReleaseAll()
        {
            for (int index = _targets.Count - 1; index >= 0; index--)
            {
                GroundBlockTarget target = _targets[index];
                if (target != null)
                {
                    target.ClearBlocker(this);
                }
            }

            _targets.Clear();
            Occupied = 0;
        }

        private void HandleStateChanged(
            RatAgent agent,
            RatState previousState,
            RatState nextState)
        {
            if (nextState != RatState.GroundCombat)
            {
                ReleaseAll();
            }
        }

        private void RemoveInvalidTargets()
        {
            for (int index = _targets.Count - 1; index >= 0; index--)
            {
                GroundBlockTarget target = _targets[index];
                if (target != null
                    && target.Agent != null
                    && !target.Agent.IsDead
                    && target.Blocker == this)
                {
                    continue;
                }

                if (target != null)
                {
                    target.ClearBlocker(this);
                }

                _targets.RemoveAt(index);
            }

            RecalculateOccupied();
        }

        private void RecalculateOccupied()
        {
            int occupied = 0;
            for (int index = 0; index < _targets.Count; index++)
            {
                GroundBlockTarget target = _targets[index];
                if (target != null)
                {
                    occupied += target.Required;
                }
            }

            Occupied = occupied;
        }
    }
}
