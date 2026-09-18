using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Cannon
{
    public sealed class CannonSlot : MonoBehaviour
    {
        [SerializeField] private VehicleSide _vehicleSide;
        [SerializeField] private CannonSlotType _slotType;
        [SerializeField] private Tilemap _slotTilemap;
        [SerializeField] private CannonSlot _targetSlot;

        public VehicleSide VehicleSide => _vehicleSide;
        public CannonSlotType SlotType => _slotType;
        public CannonSlot TargetSlot => _targetSlot;
        public Cannon InstalledCannon { get; private set; }

        private void Awake()
        {
            if (_slotTilemap == null) _slotTilemap = GetComponent<Tilemap>();
        }

        public bool TryGetWorldPosition(out Vector3 worldPosition)
        {
            if (_slotTilemap != null)
            {
                foreach (Vector3Int cellPosition in _slotTilemap.cellBounds.allPositionsWithin)
                {
                    if (!_slotTilemap.HasTile(cellPosition)) continue;
                    worldPosition = _slotTilemap.GetCellCenterWorld(cellPosition);
                    return true;
                }
            }

            worldPosition = default;
            return false;
        }

        public void RegisterCannon(Cannon cannon)
        {
            InstalledCannon = cannon;
        }

        public void UnregisterCannon(Cannon cannon)
        {
            if (InstalledCannon == cannon)
            {
                InstalledCannon = null;
            }
        }
    }
}
