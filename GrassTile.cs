using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrassCS
{
    public partial class Grass : MonoBehaviour
    {
        private struct Tile
        {
            public Terrain terrain;
            public Bounds bounds;
            public Vector2Int gridPosition;
            public float spaceMultiplier;
            public int xResolutionDivisor;
            public int zResolutionDivisor;

            public Tile(Terrain t, Bounds b, Vector2Int pos, float mul = 1, int xd = 1, int zd = 1)
            {
                terrain = t;
                bounds = b;
                gridPosition = pos;
                spaceMultiplier = mul;
                xResolutionDivisor = xd;
                zResolutionDivisor = zd;
            }
        }
    }
}
