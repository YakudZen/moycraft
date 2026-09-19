using System;
using UnityEngine;

namespace VoxelSurvival
{
    public interface IVoxelRayQuery
    {
        // False means data is not loaded: do not select blocks beyond it.
        bool TryGetSolid(Vector3Int cell, out bool solid);
    }

    public readonly struct VoxelHit
    {
        public readonly Vector3Int cell, normal;
        public readonly float distance;
        public VoxelHit(Vector3Int cell, Vector3Int normal, float distance)
        { this.cell = cell; this.normal = normal; this.distance = distance; }
        public Vector3Int Adjacent => cell + normal;
    }

    public static class VoxelRaycaster
    {
        // Traverse cells directly, independently of triangle edges and mesh colliders.
        public static bool Cast(IVoxelRayQuery world, Vector3 origin, Vector3 direction,
            float maxDistance, out VoxelHit hit)
        {
            hit = default;
            double length = Math.Sqrt((double)direction.x * direction.x +
                (double)direction.y * direction.y + (double)direction.z * direction.z);
            if (length < 1e-12 || double.IsNaN(length) || double.IsInfinity(length) ||
                maxDistance < 0 || float.IsNaN(maxDistance) || float.IsInfinity(maxDistance)) return false;
            double dx = direction.x / length, dy = direction.y / length, dz = direction.z / length;
            var cell = new Vector3Int(Initial(origin.x, dx), Initial(origin.y, dy), Initial(origin.z, dz));
            int sx = Math.Sign(dx), sy = Math.Sign(dy), sz = Math.Sign(dz);
            double tx = Crossing(origin.x, cell.x, dx), ty = Crossing(origin.y, cell.y, dy), tz = Crossing(origin.z, cell.z, dz);
            double stepX = sx == 0 ? double.PositiveInfinity : Math.Abs(1 / dx);
            double stepY = sy == 0 ? double.PositiveInfinity : Math.Abs(1 / dy);
            double stepZ = sz == 0 ? double.PositiveInfinity : Math.Abs(1 / dz);
            double distance = 0;
            Vector3Int normal = origin.x == Math.Floor(origin.x) && sx != 0 ? new Vector3Int(-sx,0,0) :
                origin.y == Math.Floor(origin.y) && sy != 0 ? new Vector3Int(0,-sy,0) :
                origin.z == Math.Floor(origin.z) && sz != 0 ? new Vector3Int(0,0,-sz) : Vector3Int.zero;
            while (distance <= maxDistance)
            {
                if (!world.TryGetSolid(cell, out bool solid)) return false;
                if (solid) { hit = new VoxelHit(cell, normal, (float)distance); return true; }
                distance = Math.Min(tx, Math.Min(ty, tz));
                if (distance > maxDistance) return false;
                // Exact corners skip cells touched only at a zero-area point.
                bool x = Math.Abs(tx - distance) < 1e-10;
                bool y = Math.Abs(ty - distance) < 1e-10;
                bool z = Math.Abs(tz - distance) < 1e-10;
                normal = x ? new Vector3Int(-sx, 0, 0) : y ? new Vector3Int(0, -sy, 0) : new Vector3Int(0, 0, -sz);
                if (x) { cell.x += sx; tx += stepX; }
                if (y) { cell.y += sy; ty += stepY; }
                if (z) { cell.z += sz; tz += stepZ; }
            }
            return false;
        }

        private static int Initial(float position, double direction)
        {
            int cell = (int)Math.Floor(position);
            return direction < 0 && position == cell ? cell - 1 : cell;
        }
        private static double Crossing(float origin, int cell, double direction) => direction == 0 ?
            double.PositiveInfinity : ((direction > 0 ? cell + 1.0 : cell) - origin) / direction;

        public static bool WithinReach(Vector3 origin, Vector3Int cell, float reach)
        {
            double x = Math.Max(0, Math.Max(cell.x - origin.x, origin.x - (cell.x + 1.0)));
            double y = Math.Max(0, Math.Max(cell.y - origin.y, origin.y - (cell.y + 1.0)));
            double z = Math.Max(0, Math.Max(cell.z - origin.z, origin.z - (cell.z + 1.0)));
            return x*x + y*y + z*z <= (double)reach * reach + 1e-6;
        }
    }
}
