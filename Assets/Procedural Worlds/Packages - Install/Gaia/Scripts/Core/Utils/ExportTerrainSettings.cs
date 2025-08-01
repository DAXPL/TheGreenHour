﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if GAIA_MESH_PRESENT
using UnityMeshSimplifierGaia;
#endif
#if PW_STORM_PRESENT
using ProceduralWorlds.Storm.Core;
using ProceduralWorlds.Storm.Utils;
#endif

namespace Gaia
{
    public enum ExportPreset { ExportToOBJFiles, ConvertToMesh, CreateImpostors, ConvertToMeshAndImpostors, ConvertToLowPolyMesh, ConvertToLowPolyMeshAndImpostors, Custom}
    public enum ExportSelection { AllTerrains, SingleTerrainOnly }
    public enum ConversionAction { MeshTerrain, ColliderOnly, OBJFileExport }
    public enum LODSettingsMode { Impostor, LowPoly, Custom }
    public enum SaveFormat { Triangles, Quads }
    public enum TerrainColliderType { MeshCollider, TerrainCollider, None }
    public enum SaveResolution { Full = 0, Half, Quarter, Eighth, Sixteenth }
    public enum NormalEdgeMode { Smooth, Sharp }
    public enum TextureExportMethod { OrthographicBake, BaseMapExport }
    public enum BakeLighting { NeutralLighting, CurrentSceneLighting }
    public enum AddAlphaChannel { None, Heightmap }
    public enum ExportedTerrainShader { Standard, VertexColor, Custom }
    public enum TextureExportResolution { x32 = 32, x64 = 64, x128 = 128, x256 = 256, x512 = 512, x1024 = 1024, x2048 = 2048, x4096 = 4096, x8192 = 8192 }
    public enum SourceTerrainTreatment { Nothing, Deactivate, StoreInBackupScenes, Delete }

    [System.Serializable]
    public class StormCapture
    {
        public bool m_isActive = true;
        public string m_fileName;
#if PW_STORM_PRESENT
        public StormQualitySettings m_stormQualitySettings;
        public StormSystemSettings m_stormSystemSettings;
#endif
        public string m_materialPropertyName;
        public string m_lastExportedTextureFileName;
        public bool m_useAlphaTransparency = false;
        public bool m_isNormalMap = false;
        public List <GameObject> m_gameObjectsToActivate = new List <GameObject>();
        public List<GameObject> m_gameObjectsToDeActivate = new List<GameObject>();
        public bool m_overrideLayerMask;
        public LayerMask m_layerMask;
    }

    [System.Serializable]
    public class ExportTerrainLODSettings
    {
        public SaveResolution m_saveResolution = SaveResolution.Half;
        public float m_simplifyQuality = 1.0f;
#if GAIA_MESH_PRESENT
        public UnityMeshSimplifierGaia.SimplificationOptions m_simplificationOptions = new UnityMeshSimplifierGaia.SimplificationOptions()
        {
            PreserveBorderEdges = true,
            PreserveUVSeamEdges = false,
            PreserveUVFoldoverEdges = false,
            PreserveSurfaceCurvature = false,
            EnableSmartLink = true,
            VertexLinkDistance = 0.0001f,
            MaxIterationCount = 100,
            Agressiveness = 7,
            ManualUVComponentCount = false,
            UVComponentCount = 2
        };
#endif

        public List<StormCapture> m_stormCaptures = new List<StormCapture>();

        public NormalEdgeMode m_normalEdgeMode = NormalEdgeMode.Smooth;
        public LODSettingsMode m_LODSettingsMode = LODSettingsMode.Impostor;
        public bool m_settingsFoldedOut = false;
        public bool m_exportTextures = true;
        public bool m_exportNormalMaps = true;
        public bool m_exportSplatmaps = true;
        public bool m_createMaterials = true;
        public ExportedTerrainShader m_materialShader = ExportedTerrainShader.Standard;
        public Shader m_customShader;
        public bool m_bakeVertexColors = true;
        public int m_VertexColorSmoothing = 3;
        public LayerMask m_bakeLayerMask = ~0; //equals "Everything"
        public TextureExportMethod m_textureExportMethod = TextureExportMethod.OrthographicBake;
        public AddAlphaChannel m_addAlphaChannel = AddAlphaChannel.Heightmap;
        public TextureExportResolution m_textureExportResolution = TextureExportResolution.x2048;
        public BakeLighting m_bakeLighting = BakeLighting.NeutralLighting;
        public string namePrefix;
        public bool m_captureBaseMapTextures = false;
        public float m_LODGroupScreenRelativeTransitionHeight = 0.8f;
        public bool m_customSimplifySettingsFoldedOut;
        public float m_HDRPLightIntensity = 5;
        public string m_customShaderDiffuseFieldName = "_Diffuse";
        public string m_customShaderNormalFieldName = "_Normal";

        public bool CompareTo(ExportTerrainLODSettings compareToLOD)
        {
            if (m_saveResolution != compareToLOD.m_saveResolution ||
            m_normalEdgeMode != compareToLOD.m_normalEdgeMode ||
            m_LODSettingsMode != compareToLOD.m_LODSettingsMode ||
            m_exportTextures != compareToLOD.m_exportTextures ||
            m_exportNormalMaps != compareToLOD.m_exportNormalMaps ||
            m_exportSplatmaps != compareToLOD.m_exportSplatmaps ||
            m_createMaterials != compareToLOD.m_createMaterials ||
            m_materialShader != compareToLOD.m_materialShader ||
            m_bakeVertexColors != compareToLOD.m_bakeVertexColors ||
            m_VertexColorSmoothing != compareToLOD.m_VertexColorSmoothing ||
            m_bakeLayerMask != compareToLOD.m_bakeLayerMask ||
            m_textureExportMethod != compareToLOD.m_textureExportMethod ||
            m_addAlphaChannel != compareToLOD.m_addAlphaChannel ||
            m_textureExportResolution != compareToLOD.m_textureExportResolution ||
            m_bakeLighting != compareToLOD.m_bakeLighting ||
            m_captureBaseMapTextures != compareToLOD.m_captureBaseMapTextures ||
            m_simplifyQuality != compareToLOD.m_simplifyQuality
#if GAIA_MESH_PRESENT
            ||
            m_simplificationOptions.PreserveBorderEdges != compareToLOD.m_simplificationOptions.PreserveBorderEdges ||
            m_simplificationOptions.PreserveUVSeamEdges != compareToLOD.m_simplificationOptions.PreserveUVSeamEdges ||
            m_simplificationOptions.PreserveUVFoldoverEdges!= compareToLOD.m_simplificationOptions.PreserveUVFoldoverEdges ||
            m_simplificationOptions.PreserveSurfaceCurvature!= compareToLOD.m_simplificationOptions.PreserveSurfaceCurvature ||
            m_simplificationOptions.EnableSmartLink != compareToLOD.m_simplificationOptions.EnableSmartLink ||
            m_simplificationOptions.VertexLinkDistance != compareToLOD.m_simplificationOptions.VertexLinkDistance ||
            m_simplificationOptions.MaxIterationCount != compareToLOD.m_simplificationOptions.MaxIterationCount ||
            m_simplificationOptions.Agressiveness!= compareToLOD.m_simplificationOptions.Agressiveness ||
            m_simplificationOptions.ManualUVComponentCount!= compareToLOD.m_simplificationOptions.ManualUVComponentCount ||
            m_simplificationOptions.UVComponentCount != compareToLOD.m_simplificationOptions.UVComponentCount
#endif
            )
            {
                return false;
            }
            return true;
        }
    }


    [System.Serializable]
    public class ExportTerrainSettings : ScriptableObject, ISerializationCallbackReceiver
    {
        //public bool m_deactivateOriginalTerrains = true;
        public SourceTerrainTreatment m_sourceTerrainTreatment = SourceTerrainTreatment.Deactivate;
        public SaveFormat m_saveFormat = SaveFormat.Triangles;
        public bool m_addTerrainCollider = true;
        public TerrainColliderType m_terrainColliderType = TerrainColliderType.TerrainCollider;
        public bool m_addMeshColliderImpostor = true;
        public ExportSelection m_exportSelection = ExportSelection.AllTerrains;
        public Texture2D m_terrainExportMask;
        public Gaia.GaiaConstants.ImageChannel m_terrainExportMaskChannel = GaiaConstants.ImageChannel.R;
        public bool m_terrainExportInvertMask = false;
        public bool m_copyGaiaGameObjects = true;
        public bool m_convertTreesToGameObjects = true;
        public bool m_copyGaiaGameObjectsImpostor = false;
        public bool m_convertSourceTerrains = false;
        public ConversionAction m_convertSourceTerrainsAction = ConversionAction.MeshTerrain;
        public SaveResolution m_colliderExportResolution = SaveResolution.Full;
        public bool m_colliderExportAddTreeColliders = true;
        public bool m_colliderExportAddGameObjectColliders = true;
        public bool m_colliderExportCreateColliderScenes = true;
        public bool m_colliderExportBakeCombinedCollisionMesh = true;
        public bool m_createImpostorScenes = false;
        public List<ExportTerrainLODSettings> m_exportTerrainLODSettingsSourceTerrains = new List<ExportTerrainLODSettings>();
        public List<ExportTerrainLODSettings> m_exportTerrainLODSettingsImpostors = new List<ExportTerrainLODSettings>();
        public string m_exportPath;
        //public LODSettingsMode m_exportPreset = LODSettingsMode.Impostor;
        public ExportPreset m_newExportPreset = ExportPreset.ConvertToMesh;
        public bool m_customSettingsFoldedOut;
        public int m_presetIndex = -99;
        public string m_lastUsedPresetName = "";
        public Mesh m_colliderTreeReplacement = null;
        public double m_impostorRange = 0;
        public float m_colliderSimplifyQuality = 1.0f;
        public bool m_customSimplificationSettingsFoldedOut;
        public bool m_colliderExportCreateServerScene;
        public bool m_debugCameraSetup = false;
        public bool m_stormCapturesOnly = false;
#if GAIA_MESH_PRESENT
        public UnityMeshSimplifierGaia.SimplificationOptions m_colliderSimplificationOptions = new UnityMeshSimplifierGaia.SimplificationOptions()
        {
            PreserveBorderEdges = true,
            PreserveUVSeamEdges = false,
            PreserveUVFoldoverEdges = false,
            PreserveSurfaceCurvature = false,
            EnableSmartLink = true,
            VertexLinkDistance = 0.0001f,
            MaxIterationCount = 100,
            Agressiveness = 7,
            ManualUVComponentCount = false,
            UVComponentCount = 2
        };
        
#endif

        public bool CompareTo(ExportTerrainSettings compareSettings)
        {
            if (m_saveFormat != compareSettings.m_saveFormat ||
            m_addTerrainCollider != compareSettings.m_addTerrainCollider ||
            m_addMeshColliderImpostor != compareSettings.m_addMeshColliderImpostor ||
            m_terrainExportMask != compareSettings.m_terrainExportMask ||
            m_terrainExportMaskChannel != compareSettings.m_terrainExportMaskChannel ||
            m_terrainExportInvertMask != compareSettings.m_terrainExportInvertMask ||
            m_convertSourceTerrainsAction != compareSettings.m_convertSourceTerrainsAction ||
            m_colliderExportResolution != compareSettings.m_colliderExportResolution ||
            m_colliderExportCreateColliderScenes != compareSettings.m_colliderExportCreateColliderScenes ||
            m_colliderExportAddTreeColliders != compareSettings.m_colliderExportAddTreeColliders ||
            m_colliderExportAddGameObjectColliders != compareSettings.m_colliderExportAddGameObjectColliders ||
            m_copyGaiaGameObjects != compareSettings.m_copyGaiaGameObjects ||
            m_convertTreesToGameObjects != compareSettings.m_convertTreesToGameObjects ||
            m_copyGaiaGameObjectsImpostor != compareSettings.m_copyGaiaGameObjectsImpostor ||
            m_convertSourceTerrains != compareSettings.m_convertSourceTerrains ||
            m_createImpostorScenes != compareSettings.m_createImpostorScenes)
                return false;

            if (m_exportTerrainLODSettingsSourceTerrains.Count != compareSettings.m_exportTerrainLODSettingsSourceTerrains.Count)
                return false;
            if (m_exportTerrainLODSettingsImpostors.Count != compareSettings.m_exportTerrainLODSettingsImpostors.Count)
                return false;

            for (int i = 0; i < m_exportTerrainLODSettingsSourceTerrains.Count; i++)
            {
                if (!m_exportTerrainLODSettingsSourceTerrains[i].CompareTo(compareSettings.m_exportTerrainLODSettingsSourceTerrains[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < m_exportTerrainLODSettingsSourceTerrains.Count; i++)
            {
                if (!m_exportTerrainLODSettingsSourceTerrains[i].CompareTo(compareSettings.m_exportTerrainLODSettingsSourceTerrains[i]))
                {
                    return false;
                }
            }

            return true;

        }

        public static void SetLODToImpostorMode(ExportTerrainLODSettings lodSettings, int LODLevel)
        {
            lodSettings.namePrefix = "LOD" + LODLevel.ToString() + "_";
            lodSettings.m_LODSettingsMode = LODSettingsMode.Impostor;
            lodSettings.m_normalEdgeMode = NormalEdgeMode.Smooth;
            lodSettings.m_exportTextures = true;
            lodSettings.m_textureExportMethod = TextureExportMethod.OrthographicBake;
            switch (LODLevel)
            {
                case 0:
                    lodSettings.m_saveResolution = SaveResolution.Half;
                    lodSettings.m_textureExportResolution = TextureExportResolution.x2048;
                    break;
                case 1:
                    lodSettings.m_saveResolution = SaveResolution.Quarter;
                    lodSettings.m_textureExportResolution = TextureExportResolution.x1024;
                    break;
                case 2:
                    lodSettings.m_saveResolution = SaveResolution.Eighth;
                    lodSettings.m_textureExportResolution = TextureExportResolution.x512;
                    break;
                default:
                    lodSettings.m_saveResolution = SaveResolution.Sixteenth;
                    lodSettings.m_textureExportResolution = TextureExportResolution.x256;
                    break;
            }
            lodSettings.m_bakeLayerMask = ~0;
            lodSettings.m_bakeLighting = BakeLighting.NeutralLighting;
            lodSettings.m_captureBaseMapTextures = false;
            lodSettings.m_bakeVertexColors = false;
            lodSettings.m_addAlphaChannel = AddAlphaChannel.None;
            lodSettings.m_exportNormalMaps = true;
            lodSettings.m_exportSplatmaps = false;
            lodSettings.m_createMaterials = true;
            lodSettings.m_materialShader = ExportedTerrainShader.Standard;
        }

        public static void SetLODToLowPolyMode(ExportTerrainLODSettings lodSettings, int LODLevel)
        {
            lodSettings.namePrefix = "LOD" + LODLevel.ToString() + "_";
            lodSettings.m_LODSettingsMode = LODSettingsMode.LowPoly;
            switch (LODLevel)
            {
                case 0:
                    lodSettings.m_saveResolution = SaveResolution.Eighth;
                    break;
                default:
                    lodSettings.m_saveResolution = SaveResolution.Sixteenth;
                    break;
            }
            lodSettings.m_normalEdgeMode = NormalEdgeMode.Sharp;
            lodSettings.m_exportTextures = true;
            lodSettings.m_textureExportMethod = TextureExportMethod.BaseMapExport;
            lodSettings.m_bakeVertexColors = true;
            lodSettings.m_VertexColorSmoothing = 3;
            lodSettings.m_addAlphaChannel = AddAlphaChannel.None;
            lodSettings.m_exportNormalMaps = false;
            lodSettings.m_exportSplatmaps = false;
            lodSettings.m_createMaterials = true;
            lodSettings.m_materialShader = ExportedTerrainShader.VertexColor;
        }

#region Serialization

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
        }
#endregion

        
    }
}