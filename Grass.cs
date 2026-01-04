using Grass;
using Clump;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace GrassCS
{
    public partial class Grass : MonoBehaviour
    {
        [SerializeField]
        private ComputeShader computeShader;

        [SerializeField]
        private Material material;

        public Camera cam;
        public Transform peopleTransform;

        //public float grassSpacing = 0.1f;
        //public int resolution = 100;

        private float grassSpacing = 0.1f;
        public float peopleSpacing = 1.5f;


        public int tileResolution = 32;
        public int tileCount = 10;


        [SerializeField, Range(0, 2)]
        public float jitterStrength;

        //public Terrain terrain;

        [Header("Culling")]
        //public float distanceCullStartDistance;
        //public float distanceCullEndDistance;
        public float distanceCullStartDisLOD0;
        public float distanceCullEndDisLOD0;
        public float distanceCullStartDisLOD1;
        public float distanceCullEndDisLOD1;
        //[Range(0f, 1f)]
        //public float distanceCullMininumGrassAmount;
        public float frustumCullNearOffset;
        public float frustumCullEdgeOffset;

        [Header("Clumping")]
        public int clumpTextureHeight;
        public int clumpTextureWidth;
        public Material clumpingVoronoiMaterial;
        public float clumpScale;
        public List<ClumpParameters> clumpParameters;


        [Header("Wind")]
        [SerializeField] private Texture2D localWindTex;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float localWindStrength = 0.5f;
        [SerializeField] private float localWindScale = 0.01f;
        [SerializeField] private float localWindSpeed = 0.1f;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float localWindRotateAmount = 0.3f;



        private ComputeBuffer grassBladesBuffer;
        private ComputeBuffer meshTrianglesBuffer;
        //private ComputeBuffer meshPositionsBuffer;
        private ComputeBuffer meshColorsBuffer;
        private ComputeBuffer meshUvsBuffer;
        private ComputeBuffer argsBuffer;
        private ComputeBuffer clumpParametersBuffer;
        private const int ARGS_STRIDE = sizeof(int) * 4;
        private Mesh clonedMesh;
        private Bounds bounds;
        private ClumpParameters[] clumpParametersArray;
        private Texture2D clumpTexture;
        private List<Tile> visibleTiles = new List<Tile>();
        private List<Tile> tilesToRender = new List<Tile>();
        private float tileSizeX = 0.0f, tileSizeZ = 0.0f;
        private List<Terrain> terrains = new List<Terrain>();

        private static readonly int
            grassBladesBufferID = Shader.PropertyToID("_GrassBlades"),
            //resolutionID = Shader.PropertyToID("_Resolution"),
            resolutionXID = Shader.PropertyToID("_ResolutionX"),
            resolutionYID = Shader.PropertyToID("_ResolutionY"),
            grassSpacingID = Shader.PropertyToID("_GrassSpacing"),
            peopleSpacingID = Shader.PropertyToID("_PeopleSpacing"),
            jitterStrengthID = Shader.PropertyToID("_JitterStrength"),
            heightMapID = Shader.PropertyToID("_HeightMap"),
            detailMapID = Shader.PropertyToID("_DetailMap"),
            terrainPositionID = Shader.PropertyToID("_TerrainPosition"),
            heightMapScaleID = Shader.PropertyToID("_HeightMapScale"),
            heightMapMultiplierID = Shader.PropertyToID("_HeightMapMultiplier"),
            distanceCullStartDistanceID = Shader.PropertyToID("_DistanceCullStartDist"),
            distanceCullEndDistanceID = Shader.PropertyToID("_DistanceCullEndDist"),
            distanceCullMininumGrassAmountID = Shader.PropertyToID("_DistanceCullMininumGrassAmount"),
            worldSpaceCameraPositionID = Shader.PropertyToID("_WSpaceCameraPos"),
            vpMatrixID = Shader.PropertyToID("_VP_MATRIX"),
            frustumCullNearOffsetID = Shader.PropertyToID("_FrustumCullNearOffset"),
            frustumCullEdgeOffsetID = Shader.PropertyToID("_FrustumCullEdgeOffset"),
            clumpParametersID = Shader.PropertyToID("_ClumpParameters"),
            numClumpParametersID = Shader.PropertyToID("_NumClumpParameters"),
            clumpTexID = Shader.PropertyToID("ClumpTex"),
            clumpScaleID = Shader.PropertyToID("_ClumpScale"),
            LocalWindTexID = Shader.PropertyToID("_LocalWindTex"),
            LocalWindScaleID = Shader.PropertyToID("_LocalWindScale"),
            LocalWindSpeedID = Shader.PropertyToID("_LocalWindSpeed"),
            LocalWindStrengthID = Shader.PropertyToID("_LocalWindStrength"),
            LocalWindRotateAmountID = Shader.PropertyToID("_LocalWindRotateAmount"),
            TimeID = Shader.PropertyToID("_Time"),
            tilePositionID = Shader.PropertyToID("_TilePosition"),
            peopleTransformID = Shader.PropertyToID("_PeopleTransform");



        void Awake()
        {
            Initialized();
            bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        }

        void Start()
        {
            
        }

        void Update()
        {
            UpdateGrassTiles();
            UpdateGpuParameters();
        }

        void LateUpdate()
        {
            RenderGrass();    
        }

        void OnDestroy()
        {
            DisposeBuffers();
            DestroyClumpTexture();
        }
        private void Initialized()
        {
            CollectTerrains();
            InitializeComputeBuffers();
            SetupMeshBuffers();
            CreateClumpTexture();
            CalculateGrassSpacing();
        }

        private void CollectTerrains()
        {
            terrains.Clear();
            Terrain[] allTerrains = FindObjectsOfType<Terrain>();
            foreach(Terrain t in allTerrains)
            {
                if (t.enabled) terrains.Add(t);
            }
            if (terrains.Count == 0)
            {
                Debug.LogWarning("Terrain count == 0");
            }
        }

        private void CalculateGrassSpacing()
        {
            //if (terrain != null)
            //{
            //    grassSpacing = terrain.terrainData.size.x / (tileCount * tileResolution);
            //}
            if(terrains.Count > 0)
            {
                grassSpacing = terrains[0].terrainData.size.x / (tileCount * tileResolution);
            }
        }


        private void InitializeComputeBuffers()
        {
            int tileMax = 16;
            //grassBladesBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 14, ComputeBufferType.Append);
            grassBladesBuffer = new ComputeBuffer(tileResolution * tileResolution * tileMax, sizeof(float) * 17, ComputeBufferType.Append);
            grassBladesBuffer.SetCounterValue(0);

            argsBuffer = new ComputeBuffer(1, ARGS_STRIDE, ComputeBufferType.IndirectArguments);

            clumpParametersBuffer = new ComputeBuffer(clumpParameters.Count, sizeof(float) * 10);
            UpdateClumpParametersBuffer();
        }

        private void UpdateGrassTiles()
        {
            tilesToRender.Clear();
            //if(terrain != null)
            //{
            //    UpdateSurroundingTilesForTerrain(terrain);
            //}
            foreach(Terrain terrain in terrains)
            {
                if(terrain != null)
                {
                    UpdateSurroundingTilesForTerrain(terrain);
                }
            }
            UpdateVisibleTiles();
        }

        private void UpdateSurroundingTilesForTerrain(Terrain terrain)
        {
            Vector3 terrainSize = terrain.terrainData.size;
            tileSizeZ = tileSizeX = terrainSize.x / tileCount;

            Vector3 cameraPositionInTerrainSpace = cam.transform.position - terrain.transform.position;
            int cameraTileXIndex = Mathf.FloorToInt(cameraPositionInTerrainSpace.x / tileSizeX);
            int cameraTileZIndex = Mathf.FloorToInt(cameraPositionInTerrainSpace.z / tileSizeZ);

            //for(int xOffset = -1; xOffset <= 2; xOffset++)
            //{
            //    for(int zOffset = -1; zOffset <= 2; zOffset++)
            //    {
            //        int tileX = cameraTileXIndex + xOffset;
            //        int tileZ = cameraTileZIndex + zOffset;
            //        if (tileX < 0 || tileZ < 0 || tileX >= tileCount || tileZ >= tileCount) continue;
            //        Bounds tileBounds = CalculateTileBounds(terrain, tileX, tileZ);
            //        tilesToRender.Add(new Tile(terrain, tileBounds, new Vector2Int(tileX, tileZ)));
            //    }
            //}
            if(cameraTileXIndex >= -4 && cameraTileXIndex < tileCount + 3 && cameraTileZIndex >= -4 && cameraTileZIndex < tileCount + 3)
            {
                HashSet<Vector2Int> mergedTileGridPositions = new HashSet<Vector2Int>();
                for(int xIndex = cameraTileXIndex - 3; xIndex <= cameraTileXIndex + 4; xIndex++)
                {
                    for(int zIndex = cameraTileZIndex - 3; zIndex <= cameraTileZIndex + 4; zIndex++)
                    {
                        Vector2Int currentGridPosition = new Vector2Int(xIndex, zIndex);
                        if(IsStandardTile(xIndex, cameraTileXIndex) && IsStandardTile(zIndex, cameraTileZIndex))
                        {
                            AddStandardTile(terrain, currentGridPosition);
                        }
                        else
                        {
                            (Vector2Int mergedTileStartPosition, bool isMerged) = CalculateMergedTileStartPoisition(xIndex, zIndex, cameraTileXIndex, cameraTileZIndex);
                            if (isMerged)
                            {
                                mergedTileGridPositions.Add(mergedTileStartPosition);
                            }
                        }
                    }
                }
                AddMergedTiles(terrain, mergedTileGridPositions);

            }
        }

        private void AddMergedTiles(Terrain terrain, HashSet<Vector2Int> mergedTileGridPositions)
        {
            foreach (Vector2Int gridPosition in mergedTileGridPositions)
            {
                if (gridPosition.x <= -2 || gridPosition.x >= tileCount || gridPosition.y <= -2 || gridPosition.y >= tileCount) continue;
                int xResolutionDivisor = 1;
                int zResolutionDivisor = 1;
                int posX = gridPosition.x;
                int posY = gridPosition.y;
                if (gridPosition.x == -1)
                {
                    xResolutionDivisor = 2;
                    posX = 0;
                }
                if (gridPosition.x == tileCount - 1)
                {
                    xResolutionDivisor = 2;
                }
                if (gridPosition.y == -1)
                {
                    zResolutionDivisor = 2;
                    posY = 0;
                }
                if (gridPosition.y == tileCount - 1)
                {
                    zResolutionDivisor = 2;
                }

                Bounds mergedBounds = CalculateTileBounds(terrain, posX, posY, 2f / xResolutionDivisor, 2f / zResolutionDivisor);
                tilesToRender.Add(new Tile(terrain, mergedBounds, new Vector2Int(posX, posY), 2f, xResolutionDivisor, zResolutionDivisor));
            }
        }

        private bool IsStandardTile(int tileIndex, int cameraTileIndex)
        {
            return tileIndex >= cameraTileIndex - 1 && tileIndex <= cameraTileIndex + 2;
        }

        private bool IsTileWithinTerrainBounds(int xIndex, int zIndex)
        {
            return xIndex >= 0 && xIndex < tileCount && zIndex >= 0 && zIndex < tileCount;
        }

        private void AddStandardTile(Terrain terrain, Vector2Int gridPosition)
        {
            Bounds tileBounds = CalculateTileBounds(terrain, gridPosition.x, gridPosition.y);
            tilesToRender.Add(new Tile(terrain, tileBounds, gridPosition, 1f, 1, 1));
        }

        private (Vector2Int, bool) CalculateMergedTileStartPoisition(int xIndex, int zIndex, int cameraTileXIndex, int cameraTileZIndex)
        {
            Vector2Int mergedStartPos = Vector2Int.zero;
            bool isMerged = false;
            if(xIndex <= cameraTileXIndex - 2)
            {
                int startZIndex = cameraTileZIndex - 3;
                int groupZIndex = (zIndex - startZIndex) / 2;
                mergedStartPos = new Vector2Int(cameraTileXIndex - 3, startZIndex + groupZIndex * 2);
                isMerged = true;
            }
            else if(xIndex >= cameraTileXIndex + 3)
            {
                int startZIndex = cameraTileZIndex - 3;
                int groupZIndex = (zIndex - startZIndex) / 2;
                mergedStartPos = new Vector2Int(cameraTileXIndex + 3, startZIndex + groupZIndex * 2);
                isMerged = true;
            }
            else if(zIndex <= cameraTileZIndex - 2 && IsStandardTile(xIndex, cameraTileXIndex))
            {
                int startXIndex = cameraTileXIndex - 1;
                int groupXIndex = (xIndex - startXIndex) / 2;
                mergedStartPos = new Vector2Int(startXIndex + groupXIndex * 2, cameraTileZIndex - 3);
                isMerged = true;
            }
            else if(zIndex >= cameraTileZIndex + 3 && IsStandardTile(xIndex, cameraTileXIndex))
            {
                int startXIndex = cameraTileXIndex - 1;
                int groupXIndex = (xIndex - startXIndex) / 2;
                mergedStartPos = new Vector2Int(startXIndex + groupXIndex * 2, cameraTileZIndex + 3);
                isMerged = true;
            }
            return (mergedStartPos, isMerged);
        }


        private Bounds CalculateTileBounds(Terrain terrain, int tileXIndex, int tileZIndex, float tileScaleX = 1.0f, float tileScaleZ = 1.0f)
        {
            Vector3 min = terrain.transform.position + new Vector3(tileXIndex * tileSizeX, -10f, tileZIndex * tileSizeZ);
            Vector3 max = min + new Vector3(tileSizeX * tileScaleX, 20f, tileSizeZ * tileScaleZ);

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private void UpdateVisibleTiles()
        {
            visibleTiles.Clear();
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
            foreach (Tile tile in tilesToRender)
            {
                if(IsVisibleInFrustum(frustumPlanes, tile.bounds))
                {
                    visibleTiles.Add(tile);
                }
            }

        }

        private bool IsVisibleInFrustum(Plane[] planes, Bounds bounds)
        {
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private void SetupMeshBuffers()
        {
            clonedMesh = GrassMesh.CreateHighLODMesh();
            clonedMesh.name = "Grass Instance Mesh";
            CreateComputeBuffersForMesh();

            argsBuffer.SetData(new int[] { meshTrianglesBuffer.count, 0, 0, 0 });
        }

        private ComputeBuffer CreateBuffer<T>(T[] data, int stride) where T : struct
        {
            ComputeBuffer buffer = new ComputeBuffer(data.Length, stride);
            buffer.SetData(data);
            return buffer;
        }

        private void CreateComputeBuffersForMesh()
        {
            int[] triangles = clonedMesh.triangles;
            //Vector3[] positions = clonedMesh.vertices;
            Color[] colors = clonedMesh.colors;
            Vector2[] uvs = clonedMesh.uv;

            meshTrianglesBuffer = CreateBuffer<int>(triangles, sizeof(int));
            //meshPositionsBuffer = CreateBuffer<Vector3>(positions, sizeof(float) * 3);
            meshColorsBuffer = CreateBuffer<Color>(colors, sizeof(float) * 4);
            meshUvsBuffer = CreateBuffer<Vector2>(uvs, sizeof(float) * 2);

            material.SetBuffer("Triangles", meshTrianglesBuffer);
            //material.SetBuffer("Positions", meshPositionsBuffer);
            material.SetBuffer("Colors", meshColorsBuffer);
            material.SetBuffer("Uvs", meshUvsBuffer);
            material.SetBuffer(grassBladesBufferID, grassBladesBuffer);
        }

        private void CreateClumpTexture()
        {
            clumpingVoronoiMaterial.SetFloat("_NumClumpTypes", clumpParameters.Count);
            RenderTexture clumpVoronoiRenderTexture = RenderTexture.GetTemporary(clumpTextureWidth, clumpTextureHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(null, clumpVoronoiRenderTexture, clumpingVoronoiMaterial, 0);

            RenderTexture.active = clumpVoronoiRenderTexture;
            clumpTexture = new Texture2D(clumpTextureWidth, clumpTextureHeight, TextureFormat.RGBAHalf, false, true);
            clumpTexture.filterMode = FilterMode.Point;
            clumpTexture.ReadPixels(new Rect(0, 0, clumpTextureWidth, clumpTextureHeight), 0, 0, true);
            clumpTexture.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(clumpVoronoiRenderTexture);
        }

        private void UpdateGpuParameters()
        {
            grassBladesBuffer.SetCounterValue(0);

            computeShader.SetVector(worldSpaceCameraPositionID, cam.transform.position);
            computeShader.SetVector(peopleTransformID, peopleTransform.position);
            computeShader.SetFloat(peopleSpacingID, peopleSpacing);

            Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
            Matrix4x4 viewProjectionMatrix = projectionMatrix * cam.worldToCameraMatrix;
            computeShader.SetMatrix(vpMatrixID, viewProjectionMatrix);
            computeShader.SetFloat(TimeID, Time.time);

            //SetupComputeShader();
            //int threadGroupsX = Mathf.CeilToInt(resolution / 8f);
            //int threadGroupsZ = Mathf.CeilToInt(resolution / 8f);
            //computeShader.Dispatch(0, threadGroupsX, threadGroupsZ, 1);

            foreach(Tile tile in visibleTiles)
            {
                SetupComputeShaderForTile(tile);
                int threadGroupsX = Mathf.CeilToInt(tileResolution / (8f * tile.xResolutionDivisor));
                int threadGroupsZ = Mathf.CeilToInt(tileResolution / (8f * tile.zResolutionDivisor));
                computeShader.Dispatch(0, threadGroupsX, threadGroupsZ, 1);
            }
        }

        private void UpdateClumpParametersBuffer()
        {
            if(clumpParameters.Count > 0)
            {
                if(clumpParametersArray == null || clumpParametersArray.Length != clumpParameters.Count)
                {
                    clumpParametersArray = new ClumpParameters[clumpParameters.Count];
                }

                clumpParameters.CopyTo(clumpParametersArray);
                clumpParametersBuffer.SetData(clumpParametersArray);
            }
        }

        private void SetupComputeShaderForTile(Tile tile)
        {
            Terrain terrain = tile.terrain;

            //computeShader.SetInt(resolutionID, tileResolution);
            computeShader.SetInt(resolutionXID, tileResolution / tile.xResolutionDivisor);
            computeShader.SetInt(resolutionYID, tileResolution / tile.zResolutionDivisor);
            float adjustedGrassSpacing = grassSpacing * tile.spaceMultiplier;
            computeShader.SetBuffer(0, grassBladesBufferID, grassBladesBuffer);
            computeShader.SetFloat(grassSpacingID, adjustedGrassSpacing);
            computeShader.SetFloat(jitterStrengthID, jitterStrength);
            computeShader.SetVector(tilePositionID, tile.bounds.min);

            computeShader.SetVector(terrainPositionID, terrain.transform.position);
            computeShader.SetTexture(0, heightMapID, terrain.terrainData.heightmapTexture);
            if (terrain.terrainData.alphamapTextures.Length > 0)
            {
                computeShader.SetTexture(0, detailMapID, terrain.terrainData.alphamapTextures[0]);
            }

            computeShader.SetFloat(heightMapScaleID, terrain.terrainData.size.x);
            computeShader.SetFloat(heightMapMultiplierID, terrain.terrainData.size.y);

            //computeShader.SetFloat(distanceCullStartDistanceID, distanceCullStartDistance);
            //computeShader.SetFloat(distanceCullEndDistanceID, distanceCullEndDistance);
            //computeShader.SetFloat(distanceCullMininumGrassAmountID, distanceCullMininumGrassAmount);
            if(tile.spaceMultiplier == 1)
            {
                computeShader.SetFloat(distanceCullStartDistanceID, distanceCullStartDisLOD0);
                computeShader.SetFloat(distanceCullEndDistanceID, distanceCullEndDisLOD0);
                computeShader.SetFloat(distanceCullMininumGrassAmountID, 0.25f);
            }
            else
            {
                computeShader.SetFloat(distanceCullStartDistanceID, distanceCullStartDisLOD1);
                computeShader.SetFloat(distanceCullEndDistanceID, distanceCullEndDisLOD1);
                computeShader.SetFloat(distanceCullMininumGrassAmountID, 0.0f);
            }
            computeShader.SetFloat(frustumCullNearOffsetID, frustumCullNearOffset);
            computeShader.SetFloat(frustumCullEdgeOffsetID, frustumCullEdgeOffset);

            UpdateClumpParametersBuffer();
            computeShader.SetBuffer(0, clumpParametersID, clumpParametersBuffer);
            computeShader.SetTexture(0, clumpTexID, clumpTexture);
            computeShader.SetFloat(clumpScaleID, clumpScale);
            computeShader.SetFloat(numClumpParametersID, clumpParameters.Count);

            computeShader.SetTexture(0, LocalWindTexID, localWindTex);
            computeShader.SetFloat(LocalWindScaleID, localWindScale);
            computeShader.SetFloat(LocalWindSpeedID, localWindSpeed);
            computeShader.SetFloat(LocalWindStrengthID, localWindStrength);
            computeShader.SetFloat(LocalWindRotateAmountID, localWindRotateAmount);

        }
        //private void SetupComputeShader()
        //{
        //    computeShader.SetInt(resolutionID, resolution);
        //    computeShader.SetFloat(grassSpacingID, grassSpacing);
        //    computeShader.SetBuffer(0, grassBladesBufferID, grassBladesBuffer);
        //    computeShader.SetFloat(jitterStrengthID, jitterStrength);

        //    if(terrain != null)
        //    {
        //        computeShader.SetVector(terrainPositionID, terrain.transform.position);
        //        computeShader.SetTexture(0, heightMapID, terrain.terrainData.heightmapTexture);
        //        if(terrain.terrainData.alphamapTextures.Length > 0)
        //        {
        //            computeShader.SetTexture(0, detailMapID, terrain.terrainData.alphamapTextures[0]);
        //        }
        //        computeShader.SetFloat(heightMapScaleID, terrain.terrainData.size.x);
        //        computeShader.SetFloat(heightMapMultiplierID, terrain.terrainData.size.y);
        //    }

        //    computeShader.SetFloat(distanceCullStartDistanceID, distanceCullStartDistance);
        //    computeShader.SetFloat(distanceCullEndDistanceID, distanceCullEndDistance);
        //    computeShader.SetFloat(distanceCullMininumGrassAmountID, distanceCullMininumGrassAmount);
        //    computeShader.SetFloat(frustumCullNearOffsetID, frustumCullNearOffset);
        //    computeShader.SetFloat(frustumCullEdgeOffsetID, frustumCullEdgeOffset);

        //    computeShader.SetVector(worldSpaceCameraPositionID, cam.transform.position);

        //    Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        //    Matrix4x4 viewProjectionMatrix = projectionMatrix * cam.worldToCameraMatrix;
        //    computeShader.SetMatrix(vpMatrixID, viewProjectionMatrix);

        //    UpdateClumpParametersBuffer();
        //    computeShader.SetBuffer(0, clumpParametersID, clumpParametersBuffer);
        //    computeShader.SetFloat(numClumpParametersID, clumpParameters.Count);
        //    computeShader.SetTexture(0, clumpTexID, clumpTexture);
        //    computeShader.SetFloat(clumpScaleID, clumpScale);


        //    computeShader.SetTexture(0, LocalWindTexID, localWindTex);
        //    computeShader.SetFloat(LocalWindScaleID, localWindScale);
        //    computeShader.SetFloat(LocalWindSpeedID, localWindSpeed);
        //    computeShader.SetFloat(LocalWindStrengthID, localWindStrength);
        //    computeShader.SetFloat(LocalWindRotateAmountID, localWindRotateAmount);
        //    computeShader.SetFloat(TimeID, Time.time);
        //}
        private void RenderGrass()
        {
            ComputeBuffer.CopyCount(grassBladesBuffer, argsBuffer, sizeof(int));
            Graphics.DrawProceduralIndirect(material, bounds, MeshTopology.Triangles, argsBuffer,
                0, null, null, UnityEngine.Rendering.ShadowCastingMode.On, true, gameObject.layer);
        }
        private void DisposeBuffers()
        {
            DisposeBuffer(grassBladesBuffer);
            DisposeBuffer(meshTrianglesBuffer);
            //DisposeBuffer(meshPositionsBuffer);
            DisposeBuffer(meshColorsBuffer);
            DisposeBuffer(meshUvsBuffer);
            DisposeBuffer(argsBuffer);
            DisposeBuffer(clumpParametersBuffer);
        }
        private void DisposeBuffer(ComputeBuffer buffer)
        {
            if(buffer != null)
            {
                buffer.Dispose();
                buffer = null;
            }
        }

        private void DestroyClumpTexture()
        {
            if(clumpTexture != null)
            {
                Destroy(clumpTexture);
                clumpTexture = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (visibleTiles == null) return;

            Color gizmoColor = Color.cyan;
            gizmoColor.a = 0.5f;
            Gizmos.color = gizmoColor;

            foreach (Tile tile in visibleTiles)
            {
                Gizmos.DrawWireCube(tile.bounds.center, tile.bounds.size);
            }
        }
    }
}