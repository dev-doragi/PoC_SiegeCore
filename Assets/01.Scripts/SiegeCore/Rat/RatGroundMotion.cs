using UnityEngine;
using UnityEngine.Tilemaps;

namespace SiegeCore.Rat
{
    public sealed class RatGroundMotion
    {
        private readonly Rigidbody2D _body;
        private readonly Collider2D _collider;
        private readonly Transform _transform;

        public RatGroundMotion(
            Rigidbody2D body,
            Collider2D collider,
            Transform transform)
        {
            _body = body;
            _collider = collider;
            _transform = transform;
        }

        public Vector2 ResolveMovement(
            Tilemap ground,
            Vector2 start,
            Vector2 end,
            out bool hitX,
            out bool hitY)
        {
            hitX = false;
            hitY = false;
            if (ground == null)
            {
                return start;
            }

            Vector3 origin = ground.GetCellCenterWorld(Vector3Int.zero);
            float cellWidth = Vector3.Distance(
                origin,
                ground.GetCellCenterWorld(Vector3Int.right));
            float cellHeight = Vector3.Distance(
                origin,
                ground.GetCellCenterWorld(Vector3Int.up));
            float step = Mathf.Max(
                0.001f,
                Mathf.Min(cellWidth, cellHeight) * 0.1f);
            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(Vector2.Distance(start, end) / step));

            Vector2 resolved = start;
            Vector2 segment = (end - start) / steps;
            bool moveX = Mathf.Abs(segment.x) > 0.000001f;
            bool moveY = Mathf.Abs(segment.y) > 0.000001f;

            for (int index = 1; index <= steps; index++)
            {
                if (moveX && !hitX)
                {
                    Vector2 candidate = resolved + new Vector2(segment.x, 0f);
                    if (IsFootprintValid(ground, candidate))
                    {
                        resolved.x = candidate.x;
                    }
                    else
                    {
                        hitX = true;
                    }
                }

                if (moveY && !hitY)
                {
                    Vector2 candidate = resolved + new Vector2(0f, segment.y);
                    if (IsFootprintValid(ground, candidate))
                    {
                        resolved.y = candidate.y;
                    }
                    else
                    {
                        hitY = true;
                    }
                }

                if ((!moveX || hitX) && (!moveY || hitY))
                {
                    break;
                }
            }

            return resolved;
        }

        public bool IsFootprintValid(Tilemap ground, Vector2 bodyPosition)
        {
            if (ground == null || _collider == null || _body == null)
            {
                return false;
            }

            Bounds bounds = _collider.bounds;
            Vector2 positionOffset = bodyPosition - _body.position;
            Vector2 center = (Vector2)bounds.center + positionOffset;
            Vector2 extents = bounds.extents;

            if (!HasGroundAt(ground, center))
            {
                return false;
            }

            return HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(-extents.x, 0f))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(extents.x, 0f))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(0f, -extents.y))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(0f, extents.y))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(-extents.x, -extents.y))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(-extents.x, extents.y))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(extents.x, -extents.y))
                && HasGroundAtFootprintPoint(ground, center, positionOffset, new Vector2(extents.x, extents.y));
        }

        public Vector2 FindNearestValidPosition(Tilemap ground, Vector2 desiredPosition)
        {
            Physics2D.SyncTransforms();
            if (ground == null || IsFootprintValid(ground, desiredPosition))
            {
                return desiredPosition;
            }

            Vector2 nearestValidPosition = desiredPosition;
            float nearestDistance = float.PositiveInfinity;
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell))
                {
                    continue;
                }

                Vector2 candidate = ground.GetCellCenterWorld(cell);
                if (!IsFootprintValid(ground, candidate))
                {
                    continue;
                }

                float distance = (candidate - desiredPosition).sqrMagnitude;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestValidPosition = candidate;
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                return desiredPosition;
            }

            Vector2 validPosition = nearestValidPosition;
            Vector2 invalidPosition = desiredPosition;
            const int interpolationSteps = 8;
            for (int index = 0; index < interpolationSteps; index++)
            {
                Vector2 candidate = Vector2.Lerp(validPosition, invalidPosition, 0.5f);
                if (IsFootprintValid(ground, candidate))
                {
                    validPosition = candidate;
                }
                else
                {
                    invalidPosition = candidate;
                }
            }

            return validPosition;
        }

        private bool HasGroundAtFootprintPoint(
            Tilemap ground,
            Vector2 predictedCenter,
            Vector2 positionOffset,
            Vector2 boundsOffset)
        {
            Vector2 currentBoundsCenter = predictedCenter - positionOffset;
            Vector2 closest = _collider.ClosestPoint(currentBoundsCenter + boundsOffset);
            Vector2 predictedPoint = closest + positionOffset;
            predictedPoint = Vector2.MoveTowards(predictedPoint, predictedCenter, 0.001f);
            return HasGroundAt(ground, predictedPoint);
        }

        private bool HasGroundAt(Tilemap ground, Vector2 point)
        {
            Vector3 worldPosition = new Vector3(point.x, point.y, _transform.position.z);
            Vector3Int cell = ground.WorldToCell(worldPosition);
            return ground.HasTile(cell);
        }
    }
}
