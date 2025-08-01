﻿using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using PWCommon5;
using Gaia.Internal;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if GAIA_MESH_PRESENT
using UnityMeshSimplifierGaia;
#endif
#if HDPipeline
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Gaia
{
    public static class ExportTerrainUtility
    {
        public static List<string> m_createdFiles = new List<string>();
        public static List<Terrain> m_processedTerrains = new List<Terrain>();
        public static List<GameObject> m_createdLODParents = new List<GameObject>();
        public static List<ExportTerrainLODSettings> m_currentLODSettingsList = new List<ExportTerrainLODSettings>();
        public static string m_workingExportPath;
        public static int m_currentTerrainCount = 0;
        public static ExportTerrainSettings m_settings;
        public static string m_copyToPath;

        private static GaiaSessionManager m_sessionManager;
        public static bool m_exportRunning;
        private static int totalCount;
        private static Vector3 terrainPos;
        private static int progressUpdateInterval = 10000;
        public static bool m_cachingWasAllowed = false;
        public static GaiaSessionManager SessionManager
        {
            get
            {
                if (m_sessionManager == null)
                {
                    m_sessionManager = GaiaSessionManager.GetSessionManager(false);
                }
                return m_sessionManager;
            }
        }

        private static void CreateColliderScene(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            //If we are in a terrain loading scenario and about to create collider scenes, we need to create the collider scene if it does not exist yet and copy the result over
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                string colliderScenePath = GetOrCreateTerrainReplacementScene(terrain, gaiaGameObjectCopyTarget, "Collider", GaiaDirectories.GetColliderScenePath(SessionManager.m_session));

                TerrainScene ts = TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Find(x => x.m_scenePath == terrain.gameObject.scene.path);
                if (ts != null)
                {
                    ts.m_colliderScenePath = colliderScenePath;
                    TerrainLoaderManager.Instance.DirtyStorageData();
                }
                else
                {
                    Debug.LogError("Could not find a terrain scene entry for terrain " + terrain.name + " Terrain Loading might not function correctly.");
                }
            }
        }
        /// <summary>
        /// Gets all terrains in the scene - including deactivated ones
        /// </summary>
        /// <returns>List of terrains</returns>
        public static List<Terrain> GetAllTerrains()
        {
            return Resources.FindObjectsOfTypeAll<Terrain>().ToList();
        }

        private static string ExportNormalMap(ExportTerrainLODSettings LODSettings, Terrain terrain)
        {
            string normalMapFileName = m_workingExportPath + Path.DirectorySeparatorChar + LODSettings.namePrefix + terrain.name + "_Normal.png";
            Texture2D normalMap = GaiaUtils.CalculateNormals(terrain);
            ImageProcessing.WriteTexture2D(normalMapFileName, normalMap);
            AssetDatabase.ImportAsset(normalMapFileName, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(normalMapFileName) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Clamp;
                AssetDatabase.ImportAsset(normalMapFileName);
            }
            return normalMapFileName;
        }

        private static string ExportTextures(ExportTerrainLODSettings LODSettings, Terrain terrain)
        {
            string textureFileName = "";
            switch (LODSettings.m_textureExportMethod)
            {
                case TextureExportMethod.BaseMapExport:
                    textureFileName = ExportBaseMap(terrain, LODSettings.namePrefix);

                    break;
                case TextureExportMethod.OrthographicBake:
                    textureFileName = m_workingExportPath + "/" + LODSettings.namePrefix + terrain.name + "_Baked";

                    //Depending on user choice we can either use the base textures or the actual textures of the terrain for the capture
                    float originalBasemapDistance = terrain.basemapDistance;
                    try
                    {
                        if (LODSettings.m_captureBaseMapTextures)
                        {
                            terrain.basemapDistance = 0;
                        }
                        else
                        {
                            //20k = max value in unity UI
                            terrain.basemapDistance = 20000;
                        }

                        //If a parent exists already, we need to deactivate it during the bake, otherwise we will bake our own mesh object that we just created before....
                        string parentName = "LODMesh" + terrain.name;
                        GameObject LODGroupParent = GameObject.Find(parentName);
                        //GameObject.Find fails in terrain loading scenario for some reason, iterate over root objects to find it
                        if (GaiaUtils.HasDynamicLoadedTerrains())
                        {
                            foreach (GameObject go in terrain.gameObject.scene.GetRootGameObjects())
                            {
                                if (go.name == parentName)
                                {
                                    LODGroupParent = go;
                                }
                            }
                        }
                        if (LODGroupParent != null)
                        {
                            LODGroupParent.SetActive(false);
                        }
                        OrthographicBake.PrepareBakeTerrain(terrain, (int)LODSettings.m_textureExportResolution, (int)LODSettings.m_textureExportResolution, LODSettings.m_bakeLayerMask, textureFileName);
                        OrthographicBake.PerformBakeNow();
                        if (LODGroupParent != null)
                        {
                            LODGroupParent.SetActive(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Error during orthographic bake. Message: " + ex.Message + " Stack Trace: " + ex.StackTrace);
                    }
                    finally
                    {
                        //Restore Basemap distance
                        terrain.basemapDistance = originalBasemapDistance;
                    }
                    textureFileName += ".png";
                    m_createdFiles.Add(textureFileName);
                    break;
            }
            AssetDatabase.Refresh();

            //set texture to repeat mode to clamp to reduce seams between meshes
            var importer = AssetImporter.GetAtPath(textureFileName) as TextureImporter;
            if (importer != null)
            {
                importer.maxTextureSize = (int)LODSettings.m_textureExportResolution;
#if HDPipeline || UPPipeline
                if (LODSettings.m_textureExportMethod == TextureExportMethod.BaseMapExport)
                {
                    importer.sRGBTexture = false;
                }
                else
                {
                    importer.sRGBTexture = true;
                }
#else
                importer.sRGBTexture = true;
#endif
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.isReadable = true;
                importer.streamingMipmaps = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                TextureImporterPlatformSettings texImpPlatSet = new TextureImporterPlatformSettings();
                texImpPlatSet.format = TextureImporterFormat.Automatic;
                texImpPlatSet.maxTextureSize = (int)LODSettings.m_textureExportResolution;
                texImpPlatSet.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(texImpPlatSet);
                AssetDatabase.ImportAsset(textureFileName);
            }

            //Open the texture again to add info in the alpha channel
            if (LODSettings.m_addAlphaChannel != AddAlphaChannel.None)
            {
                Texture2D bakedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureFileName, typeof(Texture2D));
                var colors = bakedTexture.GetPixels(0, 0, bakedTexture.width, bakedTexture.height);
                if (LODSettings.m_addAlphaChannel == AddAlphaChannel.Heightmap)
                {
                    float resDifference = (float)(terrain.terrainData.heightmapResolution - 1) / (float)bakedTexture.width;
                    var hm = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
                    for (int x = 0; x < bakedTexture.width; x++)
                    {
                        for (int y = 0; y < bakedTexture.height; y++)
                        {
                            colors[bakedTexture.width * y + x].a = hm[Mathf.RoundToInt(y * resDifference), Mathf.RoundToInt(x * resDifference)];
                        }
                    }
                }
                bakedTexture.SetPixels(colors);
                bakedTexture.Apply();
                ImageProcessing.WriteTexture2D(textureFileName, bakedTexture);
                AssetDatabase.ImportAsset(textureFileName);
            }

            //Now we can set up compression, before it would result in an error when adding the alpha channel
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            TextureImporterPlatformSettings texImpPlatSet2 = new TextureImporterPlatformSettings();
            texImpPlatSet2.format = TextureImporterFormat.Automatic;
            texImpPlatSet2.maxTextureSize = (int)LODSettings.m_textureExportResolution;
            texImpPlatSet2.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(texImpPlatSet2);
            AssetDatabase.ImportAsset(textureFileName);

            return textureFileName;
        }

        private static string GetOrCreateTerrainReplacementScene(Terrain terrain, GameObject gaiaGameObjectCopyTarget, string replacementName, string basePath)
        {
            gaiaGameObjectCopyTarget.name = terrain.name.Replace("Terrain", replacementName);
            string replacementScenePath = terrain.gameObject.scene.path.Replace("Terrain", replacementName);
            Scene replacementScene = EditorSceneManager.GetSceneByPath(replacementScenePath);
            if (replacementScene.path == null)
            {
#if GAIA_2023_PRO
                replacementScene = TerrainSceneCreator.CreateReplacementScene(terrain.gameObject.scene, gaiaGameObjectCopyTarget, SessionManager.m_session, basePath);
                EditorSceneManager.SaveScene(replacementScene);
                EditorSceneManager.CloseScene(replacementScene, true);
#endif
            }
            else
            {
                replacementScene = EditorSceneManager.OpenScene(replacementScenePath, OpenSceneMode.Additive);
                foreach (GameObject go in replacementScene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                EditorSceneManager.MoveGameObjectToScene(gaiaGameObjectCopyTarget, replacementScene);
                EditorSceneManager.SaveScene(replacementScene);
                EditorSceneManager.CloseScene(replacementScene, true);
            }
            return replacementScenePath;
        }
        private static void CreateImpostorScene(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            //If we are in a terrain loading scenario and about to create impostor scenes, we need to create the impostor scene if it does not exist yet and copy the result over
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                string impostorScenePath = GetOrCreateTerrainReplacementScene(terrain, gaiaGameObjectCopyTarget, GaiaConstants.ImpostorTerrainName, GaiaDirectories.GetImpostorScenePath(SessionManager.m_session));

                TerrainScene ts = TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Find(x => x.m_scenePath == terrain.gameObject.scene.path);
                if (ts != null)
                {
                    ts.m_impostorScenePath = impostorScenePath;
                    TerrainLoaderManager.Instance.DirtyStorageData();
                }
                else
                {
                    Debug.LogError("Could not find a terrain scene entry for terrain " + terrain.name + " Terrain Loading might not function correctly.");
                }
            }
        }
        /// <summary>
        /// Export a terrain to an OBJ file
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="LODSettings"></param>
        /// <returns></returns>
        private static string ExportToObj(Terrain terrain, Texture2D terrainMask, GaiaConstants.ImageChannel terrainMaskChannel, bool invertTerrainMask, ExportTerrainLODSettings LODSettings)
        {
            if (terrain == null)
            {
                Debug.LogWarning("WARNING: No Terrain Found. Nothing to export mesh from.");
                return null;
            }

            if (terrain.terrainData == null)
            {
                Debug.LogWarning("WARNING: Terrain Data on terrain " + terrain.name + " seems to be missing. Nothing to export the mesh from.");
                return null;
            }

            //Call original export if no mask was provided
            if (terrainMask == null)
            {
                return ExportToObj(terrain, LODSettings);
            }

            //Load up & invert the mask if necessary
            UnityHeightMap maskHm = new UnityHeightMap(terrainMask, terrainMaskChannel);
            /*
            if (invertTerrainMask)
            {
                maskHm.Invert(); 
            }
            */
            //CLYDE - FYI - this is how to get a terrain so that you can interpolate it
            UnityHeightMap terrainHm = new UnityHeightMap(terrain);

            //Do all setup for the new mesh
            string suffix = (invertTerrainMask) ? "_InvMasked" : "_Masked";
            //string fileName = GaiaDirectories.GetTerrainMeshExportDirectory(SessionManager.m_session) + "/" + LODSettings.namePrefix + terrain.name + suffix + ".obj";
            string fileName = m_workingExportPath + Path.DirectorySeparatorChar + LODSettings.namePrefix + terrain.name + suffix + ".obj";

            MaskedMeshParamters parms = new MaskedMeshParamters(
                terrainHm,
                maskHm,
                0.2f,
                (int)LODSettings.m_saveResolution,
                terrain.terrainData.size,
                (int)m_settings.m_saveFormat,
                MaskedMeshParamters.WindingOrder.CounterClockwise
                );
            try
            {
                MaskedTerrainMesh.CreateMaskedTerrainMeshes(parms, out MeshBuilder exterior, out MeshBuilder interior);
                if (invertTerrainMask)
                    exterior.Save(fileName);
                else
                    interior.Save(fileName);
            }
            catch (Exception ex)
            {
                Debug.Log("Error exporting terrain mesh: " + ex.Message);
            }
            AssetDatabase.ImportAsset(fileName);
            var modelImporter = ModelImporter.GetAtPath(fileName) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                modelImporter.weldVertices = false;
                modelImporter.importBlendShapeNormals = ModelImporterNormals.Import;
                modelImporter.importBlendShapes = false;
                //modelImporter.meshOptimizationFlags = 0;
                //modelImporter.optimizeMeshVertices = false;
                //modelImporter.optimizeMeshPolygons = false;
                if (LODSettings.m_normalEdgeMode == NormalEdgeMode.Sharp)
                {
                    modelImporter.importNormals = ModelImporterNormals.Calculate;
                    modelImporter.normalCalculationMode = ModelImporterNormalCalculationMode.Unweighted;
                    modelImporter.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
                    modelImporter.normalSmoothingAngle = 0f;
                    modelImporter.importTangents = ModelImporterTangents.None;
                }
                AssetDatabase.ImportAsset(fileName);
            }
            return fileName;
        }
        /// <summary>
        /// Exports the selected terrains with the given LODSettings
        /// </summary>
        /// <param name="LODSettings">The LOD Settings that the user entered on the UI</param>
        /// <param name="originalScene">The original scene from which the export was initiated.</param>
        /// <param name="isFinalLOD">If this is the final LOD level that is about to be created - this then triggers the LODGroup component setup</param>
        /// 
        public static void Export(List<ExportTerrainLODSettings> LODSettingsList, int index, Scene originalScene, bool isFinalLOD, bool isImpostorScenes)
        {
            ExportTerrainLODSettings LODSettings = LODSettingsList[index];
            List<Terrain> selectedTerrains = new List<Terrain>();

            int numberOfLODs = m_settings.m_convertSourceTerrains ? m_settings.m_exportTerrainLODSettingsSourceTerrains.Count : 0;
            numberOfLODs = m_settings.m_createImpostorScenes ? numberOfLODs + m_settings.m_exportTerrainLODSettingsImpostors.Count : numberOfLODs;

            if (m_settings.m_exportSelection == ExportSelection.SingleTerrainOnly)
            {
                ExportSingleTerrain(Terrain.activeTerrain, originalScene, LODSettingsList, index, numberOfLODs, isFinalLOD, isImpostorScenes);
            }
            else
            {

                if (GaiaUtils.HasDynamicLoadedTerrains())
                {
#if GAIA_2023_PRO
                    Action<Terrain> act = (t) => ExportSingleTerrain(t, originalScene, LODSettingsList, index, TerrainLoaderManager.TerrainScenes.Count() * numberOfLODs, isFinalLOD, isImpostorScenes);
                    GaiaUtils.CallFunctionOnDynamicLoadedTerrains(act, true, null, "Exporting Meshes in Terrain Scenes...");

                    //Potentially created new scenes, need to add those to build settings if not present yet.
                    if (m_settings.m_createImpostorScenes)
                    {
                        GaiaSessionManager.AddTerrainScenesToBuildSettings(TerrainLoaderManager.TerrainScenes);
                    }

#endif
                }
                else
                {
                    selectedTerrains = GetAllTerrains();
                    foreach (Terrain terrain in selectedTerrains)
                    {
                        ExportSingleTerrain(terrain, originalScene, LODSettingsList, index, numberOfLODs * selectedTerrains.Count(), isFinalLOD, isImpostorScenes);
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        public static void ExportWithLODSettings(List<ExportTerrainLODSettings> LODSettingsList, bool isImpostorScenes)
        {
            List<Light> deactivatedLights = new List<Light>();

            var originalAmbientMode = RenderSettings.ambientMode;
            var originalAmbientColor = RenderSettings.ambientSkyColor;
            var originalLODBias = QualitySettings.lodBias;

            int lodCount = 0;
            GameObject weatherObject = GameObject.Find(GaiaConstants.gaiaWeatherObject);
            foreach (ExportTerrainLODSettings LODSettings in LODSettingsList)
            {
                if (LODSettings.m_exportTextures && LODSettings.m_textureExportMethod == TextureExportMethod.OrthographicBake && LODSettings.m_bakeLighting == BakeLighting.NeutralLighting)
                {
                    //Set up neutral ambient lighting
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientSkyColor = Color.white;

                    //Increase LOD Bias to capture all trees etc. on the terrain
                    QualitySettings.lodBias = 100;

                    //Switch off all active lights in the scene as they would interfere with the baking for this mode
                    OrthographicBake.LightsOff();

                    //Switch off the water for the same reason
                    OrthographicBake.WaterOff();

#if HDPipeline
                    OrthographicBake.m_HDLODBiasOverride = 100;
                    OrthographicBake.CreateBakeDirectionalLight(5, Color.white);
#endif

                    //Do we have a weather object? Deactivate it for the baking
                    if (weatherObject != null)
                    {
                        if (weatherObject.activeInHierarchy)
                        {
                            weatherObject.SetActive(false);
                        }
                        else
                        {
                            weatherObject = null;
                        }
                    }
                }
                try
                {
                    m_exportRunning = true;
                    Scene originalScene = SessionManager.gameObject.scene;
                    Export(LODSettingsList, lodCount, originalScene, lodCount == LODSettingsList.Count - 1, isImpostorScenes);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error during Terrain Export: " + ex.Message + " Stack Trace: " + ex.StackTrace);
                }
                finally
                {
                    m_exportRunning = false;
                    lodCount++;
                    //Restore original lighting
                    OrthographicBake.LightsOn();
                    //Restore Water
                    OrthographicBake.WaterOn();
                    OrthographicBake.RemoveOrthoCam();

#if HDPipeline
                    OrthographicBake.RemoveBakeDirectionalLight();
#endif

                    if (weatherObject != null)
                    {
                        weatherObject.SetActive(true);
                    }
                    RenderSettings.ambientMode = originalAmbientMode;
                    RenderSettings.ambientSkyColor = originalAmbientColor;

                    QualitySettings.lodBias = originalLODBias;
                    ProgressBar.Clear(ProgressBarPriority.TerrainMeshExport);
                }
            }
        }

        private static GameObject SetupGameObject(List<ExportTerrainLODSettings> LODSettingsList, int index, Terrain terrain, string objFileName, string textureFileName, Material material, bool isFinalLOD, bool isImpostor)
        {
#if GAIA_MESH_PRESENT

            GameObject gaiaGameObjectCopyTarget = null;
            Texture2D bakedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureFileName, typeof(Texture2D));
            ExportTerrainLODSettings lodSettings = LODSettingsList[index];

            string parentName = GaiaConstants.MeshTerrainLODGroupPrefix + terrain.name;
            GameObject LODGroupParent = null;

            if (LODSettingsList.Count > 1)
            {
                LODGroupParent = GameObject.Find(parentName);

                //GameObject.Find fails in terrain loading scenario for some reason, iterate over root objects to find it
                if (GaiaUtils.HasDynamicLoadedTerrains())
                {
                    foreach (GameObject go in terrain.gameObject.scene.GetRootGameObjects())
                    {
                        if (go.name == parentName)
                        {
                            LODGroupParent = go;
                        }
                    }
                }
            }

            Mesh renderMesh = null;
            if ((m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider) ||
                m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain)
            {
                //Find previous level to inherit the mesh
                if (index > 0 && LODGroupParent!=null)
                {
                    foreach (Transform t in LODGroupParent.transform)
                    {
                        if (t.name.Contains("LOD" + (index - 1).ToString()))
                        {
                            MeshFilter filter = t.GetComponent<MeshFilter>();
                            if(filter!=null)
                            {
                                renderMesh = filter.sharedMesh;
                            }
                            break;
                        }
                    }
                    if (renderMesh == null)
                    {
                        renderMesh = BuildUnityMesh(terrain, lodSettings.m_saveResolution);
                    }
                }
                else
                {
                    renderMesh = BuildUnityMesh(terrain, lodSettings.m_saveResolution);
                }

                //Mesh simplification is only applied if we aim for a reduction in quality
                if (lodSettings.m_simplifyQuality<1.0f)
                {
                    UnityMeshSimplifierGaia.MeshSimplifier meshSimplifier = new UnityMeshSimplifierGaia.MeshSimplifier();
                    meshSimplifier.SimplificationOptions = lodSettings.m_simplificationOptions;
                    meshSimplifier.Initialize(renderMesh);
                    meshSimplifier.SimplifyMesh(lodSettings.m_simplifyQuality);
                    renderMesh = meshSimplifier.ToMesh();
                }
            }

            renderMesh = ProcessEdgesAndColorBaking(lodSettings, renderMesh, bakedTexture, terrain);

            GameObject newGO = new GameObject();
            newGO.layer = terrain.gameObject.layer;
            newGO.name = objFileName.Split('/').Last().Replace(".obj", "");
            //Replace slash in object name since it creates issues with navmesh generation!
            newGO.name = newGO.name.Replace("\\", "_");
            newGO.transform.position = terrain.transform.position;

            //only add a mesh filter and renderer if it is NOT a pure collider export
            if (!(m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly && !isImpostor))
            {
                MeshFilter filter = newGO.AddComponent<MeshFilter>();
                AssetDatabase.CreateAsset(renderMesh, objFileName.Replace(".obj", ".mesh"));
                filter.mesh = renderMesh;
                MeshRenderer meshRenderer = newGO.AddComponent<MeshRenderer>();
                if (material != null)
                {
                    meshRenderer.material = material;
                }
            }

            GameObject terrainExportObject = null;

            //Scene lastActiveScene = EditorSceneManager.GetActiveScene();

            //if (GaiaUtils.HasDynamicLoadedTerrains())
            //{
            //    EditorSceneManager.SetActiveScene(terrain.gameObject.scene);
            //}
            if (LODSettingsList.Count > 1)
            {

                if (LODGroupParent == null)
                {
                    LODGroupParent = new GameObject();
                    LODGroupParent.name = parentName;
                    LODGroupParent.transform.position = terrain.transform.position;
                    //With terrain loading, we want the created object to be placed in the scene of the source terrain directly.
                    //If there is no terrain loading, we want to group them under the terrain export object in the scene
                    if (GaiaUtils.HasDynamicLoadedTerrains())
                    {
                        EditorSceneManager.MoveGameObjectToScene(LODGroupParent, terrain.gameObject.scene);
                    }
                    else
                    {
                        if (terrainExportObject == null)
                        {
                            terrainExportObject = GaiaUtils.GetTerrainExportObject();
                        }
                        LODGroupParent.transform.parent = terrainExportObject.transform;
                    }
                    LODGroupParent.AddComponent<LODGroup>();
                    m_createdLODParents.Add(LODGroupParent);
                    if (m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider && !isImpostor || m_settings.m_addMeshColliderImpostor && isImpostor)
                    {
                        MeshCollider mc = LODGroupParent.AddComponent<MeshCollider>();
                        mc.sharedMesh = renderMesh;
                    }

                    if (m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.TerrainCollider && !isImpostor)
                    {
                        TerrainCollider tc = LODGroupParent.AddComponent<TerrainCollider>();
                        tc.terrainData = terrain.terrainData;
                    }
                }
                gaiaGameObjectCopyTarget = LODGroupParent;
                newGO.transform.parent = LODGroupParent.transform;
                newGO.transform.localPosition = Vector3.zero;

                if (isFinalLOD)
                {
                    LOD[] lods = new LOD[LODSettingsList.Count()];
                    for (int i = 0; i < lods.Count(); i++)
                    {
                        Renderer[] renderers = new Renderer[1];
                        renderers[0] = LODGroupParent.transform.GetChild(i).GetComponent<Renderer>();
                        lods[i] = new LOD() { fadeTransitionWidth = 0.1f, renderers = renderers, screenRelativeTransitionHeight = LODSettingsList[i].m_LODGroupScreenRelativeTransitionHeight };//((float)(lods.Count() + 1) - (float)(i + 1)) / (float)(lods.Count() + 1) };
                    }
                    LODGroupParent.GetComponent<LODGroup>().SetLODs(lods);

                }
            }
            else
            {
                //With terrain loading, we want the created object to be placed in the scene of the source terrain directly.
                //If there is no terrain loading, we want to group them under the terrain export object in the scene
                if (GaiaUtils.HasDynamicLoadedTerrains())
                {
                    EditorSceneManager.MoveGameObjectToScene(newGO, terrain.gameObject.scene);
                }
                else
                {
                    if (terrainExportObject == null)
                    {
                        terrainExportObject = GaiaUtils.GetTerrainExportObject();
                    }
                    newGO.transform.parent = terrainExportObject.transform;
                }
                //Setup the mesh collider directly on the single exported LOD in 3 cases:
                //1 We convert a source terrain to a mesh and the user wants to have a mesh collider on that, and we are currently not creating the impostor
                //2 User wants a mesh collider on the impostor, and we are creating the impostor
                //3 We are doing a collider only source terrain conversion
                if ((m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain && m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider && !isImpostor) ||
                    (m_settings.m_addMeshColliderImpostor && isImpostor) ||
                    (m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly && m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider))
                {

                    Mesh colliderMesh = renderMesh;
                    //We may re-use the render mesh, but only if
                    //1. it exists
                    //2. it does use the same resolution
                    //3. it does use the same simplification settings
                    //otherwise we need to build another mesh for the collider
                    if (renderMesh == null || 
                        m_settings.m_colliderExportResolution != lodSettings.m_saveResolution || 
                        lodSettings.m_simplifyQuality != m_settings.m_colliderSimplifyQuality ||
                        !CompareSimplificationOptions(lodSettings.m_simplificationOptions, m_settings.m_colliderSimplificationOptions))
                    {
                        colliderMesh = BuildUnityMesh(terrain, m_settings.m_colliderExportResolution);
                        //Mesh simplification is only applied if we aim for a reduction in quality
                        if (m_settings.m_colliderSimplifyQuality < 1.0f)
                        {
                            UnityMeshSimplifierGaia.MeshSimplifier meshSimplifier = new UnityMeshSimplifierGaia.MeshSimplifier();
                            meshSimplifier.SimplificationOptions = m_settings.m_colliderSimplificationOptions;
                            meshSimplifier.Initialize(colliderMesh);
                            meshSimplifier.SimplifyMesh(m_settings.m_colliderSimplifyQuality);
                            colliderMesh = meshSimplifier.ToMesh();
                        }
                    }

                    AssetDatabase.CreateAsset(colliderMesh, objFileName.Replace(".obj", "_collider.mesh"));

                    MeshCollider mc = newGO.AddComponent<MeshCollider>();
                    mc.sharedMesh = colliderMesh;
                }

                if (m_settings.m_addTerrainCollider && m_settings.m_terrainColliderType == TerrainColliderType.TerrainCollider && !isImpostor)
                {
                    TerrainCollider tc = newGO.AddComponent<TerrainCollider>();
                    tc.terrainData = terrain.terrainData;
                }

                gaiaGameObjectCopyTarget = newGO;
            }

            //if (GaiaUtils.HasDynamicLoadedTerrains())
            //{
            //    EditorSceneManager.SetActiveScene(lastActiveScene);
            //}

            return gaiaGameObjectCopyTarget;
#else
            return null;
#endif
        }
#if GAIA_MESH_PRESENT
        /// <summary>
        /// returns true if both simplifcation options use the same settings
        /// </summary>
        /// <param name="m_simplificationOptions"></param>
        /// <param name="m_colliderSimplificationOptions"></param>
        /// <returns></returns>
        private static bool CompareSimplificationOptions(SimplificationOptions simplificationOptionsA, SimplificationOptions simplificationOptionsB)
        {
            return simplificationOptionsA.Agressiveness == simplificationOptionsB.Agressiveness &&
                simplificationOptionsA.EnableSmartLink == simplificationOptionsB.EnableSmartLink &&
                simplificationOptionsA.ManualUVComponentCount == simplificationOptionsB.ManualUVComponentCount&&
                simplificationOptionsA.MaxIterationCount == simplificationOptionsB.MaxIterationCount &&
                simplificationOptionsA.PreserveBorderEdges == simplificationOptionsB.PreserveBorderEdges &&
                simplificationOptionsA.PreserveSurfaceCurvature == simplificationOptionsB.PreserveSurfaceCurvature &&
                simplificationOptionsA.PreserveUVFoldoverEdges == simplificationOptionsB.PreserveUVFoldoverEdges &&
                simplificationOptionsA.PreserveUVSeamEdges == simplificationOptionsB.PreserveUVSeamEdges &&
                simplificationOptionsA.UVComponentCount == simplificationOptionsB.UVComponentCount &&
                simplificationOptionsA.VertexLinkDistance == simplificationOptionsB.VertexLinkDistance;
        }
#endif

        /// <summary>
        /// Read a texture from a color array according to x-z coordinates from the terrain without exceeding the index
        /// </summary>
        /// <param name="colors">Color Array to read from</param>
        /// <param name="max">max position value for x / z</param>
        /// <param name="x">x-coordinate from the terrain</param>
        /// <param name="z">z-coordinate from the terrain</param>
        /// <returns></returns>
        private static Color GetSafeColor(Color[] colors, int max, int x, int z)
        {
            int maxIndex = max - 1;
            int safeZ = Mathf.Clamp(z, 0, maxIndex);
            int safeX = Mathf.Clamp(x, 0, maxIndex);
            int safeIndex = safeZ * max + safeX;
            return colors[safeIndex];
        }
        private static Texture2D Smooth(Texture2D input, int iterations)
        {
            Color[] colors = input.GetPixels();

            for (int i = 0; i < iterations; i++)
            {
                Color[] workingcolors = new Color[colors.Length];
                for (int j = 0; j < colors.Length; j++)
                {
                    workingcolors[j] = new Color(colors[j].r, colors[j].g, colors[j].b, colors[j].a);
                }

                for (int x = 0; x < input.width; x++)
                {
                    for (int z = 0; z < input.height; z++)
                    {
                        int index = z * input.width + x;
                        workingcolors[index] = (GetSafeColor(colors, input.width, x, z - 1) + GetSafeColor(colors, input.width, x, z + 1) + GetSafeColor(colors, input.width, x - 1, z) + GetSafeColor(colors, input.width, x + 1, z)) / 4f;
                    }
                }
                colors = workingcolors;
            }

            Texture2D returnTexture = new Texture2D(input.width, input.height);
            returnTexture.SetPixels(colors);
            returnTexture.Apply();
            return returnTexture;
        }

        private static Mesh BuildUnityMesh(Terrain terrain, SaveResolution resolution)
        {
            terrainPos = terrain.transform.position;
            int w = terrain.terrainData.heightmapResolution;
            int h = terrain.terrainData.heightmapResolution;
            Vector3 meshScale = terrain.terrainData.size;
            int tRes = (int)Mathf.Pow(2, (int)resolution);
            meshScale = new Vector3(meshScale.x / (w - 1) * tRes, meshScale.y, meshScale.z / (h - 1) * tRes);
            Vector2 uvScale = new Vector2(1.0f / (w - 1), 1.0f / (h - 1));
            float[,] tData = terrain.terrainData.GetHeights(0, 0, w, h);

            w = (w - 1) / tRes + 1;
            h = (h - 1) / tRes + 1;
            Vector3[] tVertices = new Vector3[w * h];

            Vector2[] tUV = new Vector2[w * h];
            int[] tPolys;
            tPolys = new int[(w - 1) * (h - 1) * 6];


            // Build vertices and UVs
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    //tVertices[y * w + x] = Vector3.Scale(meshScale, new Vector3(y, tData[x * tRes, y * tRes], x));// - terrain.transform.position;
                    tVertices[y * w + x] = Vector3.Scale(meshScale, new Vector3(y, tData[x * tRes, y * tRes], x));
                    tUV[y * w + x] = Vector2.Scale(new Vector2(y * tRes, x * tRes), uvScale);
                }
            }

            int index = 0;

            // Build triangle indices: 3 indices into vertex array for each triangle
            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    // For each grid cell output two triangles
                    tPolys[index++] = (y * w) + x;
                    tPolys[index++] = ((y + 1) * w) + x;
                    tPolys[index++] = (y * w) + x + 1;

                    tPolys[index++] = ((y + 1) * w) + x;
                    tPolys[index++] = ((y + 1) * w) + x + 1;
                    tPolys[index++] = (y * w) + x + 1;
                }
            }

            int numPolys = tPolys.Length - 1;

            int[] tPolysFlipped = new int[tPolys.Length];
            for (int i = numPolys; i >= 0; i--)
            {
                tPolysFlipped[i] = tPolys[numPolys - i];
            }
            tPolys = tPolysFlipped;

            Mesh returnMesh = new Mesh();
            returnMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            returnMesh.vertices = tVertices;
            returnMesh.uv = tUV;
            returnMesh.triangles = tPolys;

            returnMesh.RecalculateNormals();

            return returnMesh;
        }


        private static Mesh ProcessEdgesAndColorBaking(ExportTerrainLODSettings LODSettings, Mesh mesh, Texture2D bakedTexture, Terrain terrain )
        {
            Vector3[] flatVertices = new Vector3[0];
            Vector2[] flatUVs = new Vector2[0];
            Color[] dVCs = new Color[0];
            Color[] textureColors = new Color[0];
            int[] tPolys = mesh.triangles;
            Vector3[] tVertices = mesh.vertices;
            Vector2[] tUV = mesh.uv;

            int mipLevel = 0;
            int mipWidth = 0;
            if (bakedTexture != null)
            {
                mipWidth = Math.Max(1, bakedTexture.width >> mipLevel);
            }

            if (LODSettings.m_bakeVertexColors && bakedTexture != null)
            {
                if (LODSettings.m_VertexColorSmoothing > 0)
                {
                    Texture2D smoothTexture = Smooth(bakedTexture, LODSettings.m_VertexColorSmoothing);
                    textureColors = smoothTexture.GetPixels(mipLevel);
                }
                else
                {
                    textureColors = bakedTexture.GetPixels(mipLevel);
                }
            }

            if (LODSettings.m_normalEdgeMode == NormalEdgeMode.Sharp)
            {
#if GAIA_2023_PRO
                LowPolyHelper.FlattenPolysAndUVs(ref flatVertices, ref flatUVs, ref tPolys, tVertices, tUV);
                //flatVertices = new Vector3[tPolys.Length];
                int numPolys = tPolys.Length - 1;
                int[] tPolysFlipped = new int[tPolys.Length];
                for (int i = numPolys; i >= 0; i--)
                {
                    tPolysFlipped[i] = tPolys[numPolys - i];
                }
                tPolys = tPolysFlipped;

                if (LODSettings.m_bakeVertexColors && bakedTexture != null)
                {
                    dVCs = LowPolyHelper.BakeSharpVertexColorsToArray(flatVertices, tPolys, textureColors, mipWidth, terrain, LODSettings);
                }
#endif
            }
            else
            {

#if GAIA_2023_PRO
                if (LODSettings.m_bakeVertexColors && bakedTexture != null)
                {
                    dVCs = LowPolyHelper.BakeSmoothVertexColorsToArray(tVertices, textureColors, mipWidth, terrain, LODSettings);
                }
#endif
            }

            Mesh returnMesh = new Mesh();
            returnMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            if (LODSettings.m_normalEdgeMode == NormalEdgeMode.Sharp)
            {
                returnMesh.vertices = flatVertices;
                returnMesh.uv = flatUVs;
            }
            else
            {
                returnMesh.vertices = tVertices;
                returnMesh.uv = tUV;
            }
            returnMesh.triangles = tPolys;

            if (LODSettings.m_bakeVertexColors && bakedTexture != null)
            {
                returnMesh.colors = dVCs;
            }

            returnMesh.RecalculateNormals();

           return returnMesh;

        }


        /// <summary>
        /// Export a terrain to an OBJ file
        /// </summary>
        /// <param name="terrain">The terrain to export</param>
        /// <param name="LODSettings">The Export LOD Settings</param>
        /// <returns></returns>
        private static string ExportToObj(Terrain terrain, ExportTerrainLODSettings LODSettings)
        {
            string fileName = m_workingExportPath + Path.DirectorySeparatorChar + LODSettings.namePrefix + terrain.name + ".obj";
            terrainPos = terrain.transform.position;
            int w = terrain.terrainData.heightmapResolution;
            int h = terrain.terrainData.heightmapResolution;
            Vector3 meshScale = terrain.terrainData.size;
            int tRes = (int)Mathf.Pow(2, (int)LODSettings.m_saveResolution);
            meshScale = new Vector3(meshScale.x / (w - 1) * tRes, meshScale.y, meshScale.z / (h - 1) * tRes);
            Vector2 uvScale = new Vector2(1.0f / (w - 1), 1.0f / (h - 1));
            float[,] tData = terrain.terrainData.GetHeights(0, 0, w, h);

            w = (w - 1) / tRes + 1;
            h = (h - 1) / tRes + 1;
            Vector3[] tVertices = new Vector3[w * h];

            Vector2[] tUV = new Vector2[w * h];
            int[] tPolys;

            if (m_settings.m_saveFormat == SaveFormat.Triangles)
            {
                tPolys = new int[(w - 1) * (h - 1) * 6];
                //Vector3[] tNormals = new Vector3[tPolys.Length * 6];
            }
            else
            {
                tPolys = new int[(w - 1) * (h - 1) * 4];
                //Vector3[] tNormals = new Vector3[tPolys.Length * 4];
            }



            // Build vertices and UVs
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tVertices[y * w + x] = Vector3.Scale(meshScale, new Vector3(-y, tData[x * tRes, y * tRes], x));
                    tUV[y * w + x] = Vector2.Scale(new Vector2(x * tRes, y * tRes), uvScale);
                }
            }

            int index = 0;
            if (m_settings.m_saveFormat == SaveFormat.Triangles)
            {
                // Build triangle indices: 3 indices into vertex array for each triangle
                for (int y = 0; y < h - 1; y++)
                {
                    for (int x = 0; x < w - 1; x++)
                    {
                        // For each grid cell output two triangles
                        tPolys[index++] = (y * w) + x;
                        tPolys[index++] = ((y + 1) * w) + x;
                        tPolys[index++] = (y * w) + x + 1;

                        //calculate normal for this face
                        Vector3 v1 = tVertices[((y + 1) * w) + x] - tVertices[(y * w) + x];
                        Vector3 v2 = tVertices[(y * w) + x + 1] - tVertices[(y * w) + x];
                        Vector3 normal = Vector3.Cross(v1, v2);
                        //tNormals[index] = normal;
                        //tNormals[index-1] = normal;
                        //tNormals[index-2] = normal;

                        tPolys[index++] = ((y + 1) * w) + x;
                        tPolys[index++] = ((y + 1) * w) + x + 1;
                        tPolys[index++] = (y * w) + x + 1;

                        //calculate normal for that face
                        v1 = tVertices[((y + 1) * w) + x + 1] - tVertices[((y + 1) * w) + x];
                        v2 = tVertices[(y * w) + x + 1] - tVertices[((y + 1) * w) + x];
                        normal = Vector3.Cross(v1, v2);
                        //tNormals[index] = normal;
                        //tNormals[index - 1] = normal;
                        //tNormals[index - 2] = normal;
                    }
                }
            }
            else
            {
                // Build quad indices: 4 indices into vertex array for each quad
                for (int y = 0; y < h - 1; y++)
                {
                    for (int x = 0; x < w - 1; x++)
                    {
                        // For each grid cell output one quad
                        tPolys[index++] = (y * w) + x;
                        tPolys[index++] = ((y + 1) * w) + x;
                        tPolys[index++] = ((y + 1) * w) + x + 1;
                        tPolys[index++] = (y * w) + x + 1;
                    }
                }
            }

            // Export to .obj
            StreamWriter sw = new StreamWriter(fileName);
            try
            {
                sw.WriteLine("# Unity terrain OBJ File");

                // Write vertices
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                totalCount = (tVertices.Length * 2 + (m_settings.m_saveFormat == SaveFormat.Triangles ? tPolys.Length / 3 : tPolys.Length / 4)) / progressUpdateInterval;
                for (int i = 0; i < tVertices.Length; i++)
                {
                    //UpdateProgress();
                    StringBuilder sb = new StringBuilder("v ", 20);
                    // StringBuilder stuff is done this way because it's faster than using the "{0} {1} {2}"etc. format
                    // Which is important when you're exporting huge terrains.
                    sb.Append(tVertices[i].x.ToString()).Append(" ").
                        Append(tVertices[i].y.ToString()).Append(" ").
                        Append(tVertices[i].z.ToString());
                    sw.WriteLine(sb);
                }
                // Write UVs
                for (int i = 0; i < tUV.Length; i++)
                {
                    //UpdateProgress();
                    StringBuilder sb = new StringBuilder("vt ", 22);
                    sb.Append(tUV[i].y.ToString()).Append(" ").
                        Append(tUV[i].x.ToString());
                    sw.WriteLine(sb);
                }
                //// Write Normals
                //for (int i = 0; i < tNormals.Length; i++)
                //{
                //    //UpdateProgress();
                //    StringBuilder sb = new StringBuilder("vn ", 22);
                //    sb.Append(tNormals[i].x.ToString()).Append(" ").
                //        Append(tNormals[i].y.ToString()).Append(" ").
                //        Append(tNormals[i].z.ToString());
                //    sw.WriteLine(sb);
                //}
                if (m_settings.m_saveFormat == SaveFormat.Triangles)
                {
                    // Write triangles
                    for (int i = 0; i < tPolys.Length; i += 3)
                    {
                        //UpdateProgress();
                        StringBuilder sb = new StringBuilder("f ", 43);
                        sb.Append(tPolys[i] + 1).Append("/").Append(tPolys[i] + 1).Append(" ").
                            Append(tPolys[i + 1] + 1).Append("/").Append(tPolys[i + 1] + 1).Append(" ").
                            Append(tPolys[i + 2] + 1).Append("/").Append(tPolys[i + 2] + 1);
                        sw.WriteLine(sb);
                    }
                }
                else
                {
                    // Write quads
                    for (int i = 0; i < tPolys.Length; i += 4)
                    {
                        //UpdateProgress();
                        StringBuilder sb = new StringBuilder("f ", 57);
                        sb.Append(tPolys[i] + 1).Append("/").Append(tPolys[i] + 1).Append(" ").
                            Append(tPolys[i + 1] + 1).Append("/").Append(tPolys[i + 1] + 1).Append(" ").
                            Append(tPolys[i + 2] + 1).Append("/").Append(tPolys[i + 2] + 1).Append(" ").
                            Append(tPolys[i + 3] + 1).Append("/").Append(tPolys[i + 3] + 1);
                        sw.WriteLine(sb);
                    }
                }
            }
            catch (Exception err)
            {
                Debug.Log("Error saving file: " + err.Message);
            }
            sw.Close();
            AssetDatabase.ImportAsset(fileName);
            return fileName;
        }


        /// <summary>
        /// Resize the supplied texture, also handles non rw textures and makes them rm
        /// </summary>
        /// <param name="texture">Source texture</param>
        /// <param name="width">Width of new texture</param>
        /// <param name="height">Height of new texture</param>
        /// <param name="mipmap">Generate mipmaps</param>
        /// <param name="linear">Use linear colour conversion</param>
        /// <returns>New texture</returns>
        public static Texture2D ResizeTexture(Texture2D texture, TextureFormat format, int aniso, int width, int height, bool mipmap, bool linear, bool compress)
        {
            RenderTexture rt;
            if (linear)
            {
                rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            }
            else
            {
                rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            }
            bool prevRgbConversionState = GL.sRGBWrite;
            if (linear)
            {
                GL.sRGBWrite = false;
            }
            else
            {
                GL.sRGBWrite = true;
            }
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D newTexture = new Texture2D(width, height, format, mipmap, linear);
            newTexture.name = texture.name + " X";
            newTexture.anisoLevel = aniso;
            newTexture.filterMode = texture.filterMode;
            newTexture.wrapMode = texture.wrapMode;
            newTexture.mipMapBias = texture.mipMapBias;
            newTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            newTexture.Apply(true);

            if (compress)
            {
                newTexture.Compress(true);
                newTexture.Apply(true);
            }

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            GL.sRGBWrite = prevRgbConversionState;
            return newTexture;
        }
        /// <summary>
        /// Export the selected splatmap texture as a PNG or all
        /// </summary>
        /// <param name="path">Path to save it as</param>
        /// <param name="textureIdx">The texture to save</param>
        public static string ExportSplatmap(Terrain terrain, string LODPrefix)
        {
            if (terrain == null)
            {
                Debug.LogError("No terrain, unable to export splatmaps");
                return "";
            }

            string path = m_workingExportPath + Path.DirectorySeparatorChar + LODPrefix + terrain.name + "_SplatMap";

            int width = terrain.terrainData.alphamapWidth;
            int height = terrain.terrainData.alphamapHeight;
            int layers = terrain.terrainData.alphamapLayers;

            float[,,] splatMaps = terrain.terrainData.GetAlphamaps(0, 0, width, height);

            GaiaUtils.CompressToMultiChannelFileImage(splatMaps, path, TextureFormat.RGBA32, true, true, false);

            return path;
        }

        private static string ExportBaseMap(Terrain terrain, string LODPrefix)
        {
            if (terrain.terrainData.alphamapTextures.Length <= 0)
            {
                //No textures, no base map
                Debug.LogWarning("No textures found on terrain '" + terrain.name + "', skipping base map export.");
                return "";
            }

            string fname = m_workingExportPath + "/" + LODPrefix + terrain.name + "_BaseMap";
            // fname = Path.Combine(path, PWCommon5.Utils.FixFileName(mgr.PhysicalTerrainArray[tileX, tileZ].name + "_BaseMap"));

            Texture2D[] terrainSplats = terrain.terrainData.alphamapTextures;

            GaiaSplatPrototype[] terrainSplatPrototypes = GaiaSplatPrototype.GetGaiaSplatPrototypes(terrain);
            int width = terrainSplats[0].width;
            int height = terrainSplats[0].height;
            float dimensions = width * height;

            //Get the average colours of the terrain textures by using the highest mip
            Color[] averageSplatColors = new Color[terrainSplatPrototypes.Length];
            for (int protoIdx = 0; protoIdx < terrainSplatPrototypes.Length; protoIdx++)
            {
                GaiaSplatPrototype proto = terrainSplatPrototypes[protoIdx];

                Texture2D tmpTerrainTex = ResizeTexture(proto.texture, TextureFormat.ARGB32, 8, width, height, true, true, false);
                Color[] maxMipColors = tmpTerrainTex.GetPixels(tmpTerrainTex.mipmapCount - 1);
                averageSplatColors[protoIdx] = new Color(maxMipColors[0].r, maxMipColors[0].g, maxMipColors[0].b, maxMipColors[0].a);
            }


            //Create the new texture
            Texture2D colorTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            colorTex.name = terrain.name + "_BaseMap";
            colorTex.wrapMode = TextureWrapMode.Repeat;
            colorTex.filterMode = FilterMode.Bilinear;
            colorTex.anisoLevel = 8;
            float xInv = 1f / width;
            float zInv = 1f / height;
            for (int x = 0; x < width; x++)
            {
                //if (x % 250 == 0)
                //{
                //    EditorUtility.DisplayProgressBar("Baking Textures", "Ingesting terrain basemap : " + terrain.name + "..", (float)(x * width) / dimensions);
                //}

                for (int z = 0; z < height; z++)
                {
                    int splatColorIdx = 0;
                    Color mapColor = Color.black;
                    for (int splatIdx = 0; splatIdx < terrainSplats.Length; splatIdx++)
                    {
                        Texture2D terrainSplat = terrainSplats[splatIdx];
                        Color splatColor;
                        splatColor = terrainSplat.GetPixel(x, z);


                        if (splatColorIdx < averageSplatColors.Length)
                        {
                            mapColor = Color.Lerp(mapColor, averageSplatColors[splatColorIdx++], splatColor.r);
                        }
                        if (splatColorIdx < averageSplatColors.Length)
                        {
                            mapColor = Color.Lerp(mapColor, averageSplatColors[splatColorIdx++], splatColor.g);
                        }
                        if (splatColorIdx < averageSplatColors.Length)
                        {
                            mapColor = Color.Lerp(mapColor, averageSplatColors[splatColorIdx++], splatColor.b);
                        }
                        if (splatColorIdx < averageSplatColors.Length)
                        {
                            mapColor = Color.Lerp(mapColor, averageSplatColors[splatColorIdx++], splatColor.a);
                        }
                        //if (alphaMask != null)
                        //{
                        //    mapColor.a = alphaMask[xInv * x, zInv * z];
                        //}
                        //else
                        //{
                        //mapColor.a = 1f;
                        //}
                    }
#if HDPipeline || UPPipeline
                    colorTex.SetPixel(x, z, mapColor);
#else
                    colorTex.SetPixel(x, z, mapColor.gamma);
#endif
                }
            }
            colorTex.Apply();

            //EditorUtility.DisplayProgressBar("Baking Textures", "Encoding terrain basemap : " + terrain.name + "..", 0f);

            //Save it
            byte[] content = colorTex.EncodeToPNG();
            fname += ".png";
            File.WriteAllBytes(fname, content);

            //AssetDatabase.ImportAsset(fname);

            //Shut it up
            //EditorUtility.ClearProgressBar();

            return fname;
        }
        private static string CreateMaterial(ExportTerrainLODSettings LODSettings, out Material mat, string objFileName, string textureFileName, string normalMapFileName, bool isImpostor)
        {
            if (LODSettings.m_materialShader == ExportedTerrainShader.Standard)
            {
                mat = new Material(Shader.Find("Standard"));
#if UPPipeline
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
#elif HDPipeline
                mat = new Material(Shader.Find("HDRP/Lit"));
#endif
            }
            else
            {
#if HDPipeline || UPPipeline
                mat = new Material(Shader.Find("Shader Graphs/PW_Vertex_Color_SRP"));
#else
                mat = new Material(Shader.Find("PWS/PW_VertexColor"));
#endif

            }
            string matFileName = objFileName;
            //need to make sure the file name is different for impostors so they do not share the filename with a potential source terrain mesh conversion
            if (isImpostor)
            {
                matFileName = objFileName.Replace(".obj", "_Impostor.mat");
            }
            else
            {
                matFileName = objFileName.Replace(".obj", ".mat");
            }




            if (LODSettings.m_exportTextures)
            {
                Texture2D texture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureFileName, typeof(Texture2D));
#if UPPipeline
                mat.SetTexture("_BaseMap", texture);
#elif HDPipeline
                mat.SetTexture("_BaseColorMap", texture);
#else
                mat.SetTexture("_MainTex", texture);
#endif
            }

            if (LODSettings.m_exportNormalMaps)
            {
                Texture2D normalMap = (Texture2D)AssetDatabase.LoadAssetAtPath(normalMapFileName, typeof(Texture2D));
#if HDPipeline
                mat.SetTexture("_NormalMap", normalMap);
                //Sets normal map to Object Space
                mat.SetFloat("_NormalMapSpace", 1f);
#else
                mat.SetFloat("_Glossiness", 0f);
                mat.SetTexture("_BumpMap", normalMap);
                float normalStrength = 1f;
                mat.SetFloat("_BumpScale", normalStrength);
                mat.EnableKeyword("_NORMALMAP");
#endif
            }

#if UPPipeline
            mat.SetFloat("_Smoothness", 0f);
#elif HDPipeline
            mat.SetFloat("_Smoothness", 0f);

#else
            mat.SetFloat("_Glossiness", 0f);
#endif
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matFileName);
            AssetDatabase.ImportAsset(matFileName, ImportAssetOptions.ForceUpdate);
            return matFileName;
        }


        private static void CopyGaiaSpawns(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            if (gaiaGameObjectCopyTarget != null)
            {
                Transform spawnedGOTransform = terrain.transform.Find(GaiaConstants.defaultGOSpawnTarget);
                if (spawnedGOTransform != null)
                {
                    GameObject duplicate = UnityEngine.Object.Instantiate(spawnedGOTransform.gameObject);
                    duplicate.transform.parent = gaiaGameObjectCopyTarget.transform;
                    duplicate.transform.localPosition = spawnedGOTransform.transform.localPosition;
                }
            }
        }

        private static void AddGameObjectColliders(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            Collider[] allChildColliders = terrain.gameObject.GetComponentsInChildren<Collider>();
            if (m_settings.m_colliderExportBakeCombinedCollisionMesh)
            {
                List<CombineInstance> combineInstances = new List<CombineInstance>();
                //Add the existing mesh in as the first instance
                //UnityEngine.Matrix4x4 MCmatrix = UnityEngine.Matrix4x4.TRS(mainMC.transform.position - terrain.transform.position, mainMC.transform.rotation, mainMC.transform.lossyScale);
                //combineInstances.Add(new CombineInstance() { mesh = mainMC.sharedMesh, transform = MCmatrix });
                Mesh builtinCubeMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Cube");
                Mesh builtinCapsuleMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Capsule");
                Mesh builtinSphereMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Sphere");

                for (int layer = 0; layer < 31; layer++)
                {
                    var allCollidersOnLayer = allChildColliders.Where(x => x.gameObject.layer == layer).ToArray();
                    combineInstances.Clear();

                    for (int i = 0; i < allCollidersOnLayer.Length; i++)
                    {
                        Mesh mesh = null;
                        Type type = allCollidersOnLayer[i].GetType();
                        Vector3 scale = Vector3.one;
                        Vector3 position = allCollidersOnLayer[i].transform.position;
                        Quaternion rotation = allCollidersOnLayer[i].transform.rotation;
                        if (type == typeof(TerrainCollider))
                        {
                            continue;
                        }
                        if (type == typeof(BoxCollider))
                        {
                            BoxCollider box = (BoxCollider)allCollidersOnLayer[i];
                            mesh = builtinCubeMesh;
                            scale = Vector3.Scale(box.size, box.transform.lossyScale);
                            position = allCollidersOnLayer[i].bounds.center;
                        }
                        if (type == typeof(CapsuleCollider))
                        {
                            mesh = builtinCapsuleMesh;

                            CapsuleCollider capsuleCollider = (CapsuleCollider)allCollidersOnLayer[i];

                            float radiusScaleFactor = 0f;
                            Vector3 sizeScale = capsuleCollider.transform.lossyScale;

                            for (int axis = 0; axis < 3; ++axis)
                            {
                                if (axis != capsuleCollider.direction)
                                    radiusScaleFactor = Mathf.Max(radiusScaleFactor, Mathf.Abs(sizeScale[axis]));
                            }

                            for (int axis = 0; axis < 3; ++axis)
                            {
                                if (axis != capsuleCollider.direction)
                                    sizeScale[axis] = Mathf.Sign(sizeScale[axis]) * radiusScaleFactor;
                            }

                            scale.y = 0.5f * capsuleCollider.height * Mathf.Abs(sizeScale[capsuleCollider.direction]);
                            scale.x = 2f * capsuleCollider.radius * radiusScaleFactor;
                            scale.z = 2f * capsuleCollider.radius * radiusScaleFactor;

                            position = allCollidersOnLayer[i].bounds.center;

                            switch (capsuleCollider.direction)
                            {
                                case 0: //X
                                    rotation *= Quaternion.Euler(0f, 0f, 90f);
                                    break;
                                case 1: //Y
                                        //Nothing to do, the capsule mesh is oriented towards Y already
                                    break;
                                case 2: //Z
                                    rotation *= Quaternion.Euler(90f, 0f, 0f);
                                    break;
                            }

                        }
                        if (type == typeof(SphereCollider))
                        {
                            mesh = builtinSphereMesh;
                            scale = allCollidersOnLayer[i].bounds.size;
                        }
                        if (type == typeof(MeshCollider))
                        {
                            mesh = ((MeshCollider)allCollidersOnLayer[i]).sharedMesh;
                            scale = allCollidersOnLayer[i].transform.localScale;
                        }

                        position -= terrain.transform.position;

                        UnityEngine.Matrix4x4 matrix = UnityEngine.Matrix4x4.TRS(position, rotation, scale);
                        combineInstances.Add(new CombineInstance() { mesh = mesh, transform = matrix });
                    }
                    if (combineInstances.Count > 0)
                    {
                        Mesh combinedMesh = new Mesh();
                        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        combinedMesh.CombineMeshes(combineInstances.ToArray());
                        GameObject layerGO = new GameObject("Game Objects-" + LayerMask.LayerToName(layer));
                        layerGO.layer = layer;
                        MeshCollider layerCollider = layerGO.AddComponent<MeshCollider>();
                        layerCollider.sharedMesh = combinedMesh;
                        layerCollider.transform.position = gaiaGameObjectCopyTarget.transform.position;
                        layerCollider.transform.parent = gaiaGameObjectCopyTarget.transform;
                    }
                }

            }
            else
            {
                List<GameObject> createdGameObjects = new List<GameObject>();
                foreach (Collider coll in allChildColliders)
                {
                    if (coll.GetType() == typeof(TerrainCollider))
                    {
                        continue;
                    }
                    GameObject collGO = new GameObject();
                    collGO.transform.position = coll.gameObject.transform.position;
                    collGO.transform.rotation = coll.gameObject.transform.rotation;
                    collGO.transform.localScale = coll.gameObject.transform.localScale;
                    collGO.name = coll.gameObject.name;
                    collGO.tag = coll.gameObject.tag;
                    collGO.layer = coll.gameObject.layer;
                    UnityEditorInternal.ComponentUtility.CopyComponent(coll);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(collGO);
                    createdGameObjects.Add(collGO);
                }

                foreach (GameObject go in createdGameObjects)
                {
                    go.transform.parent = gaiaGameObjectCopyTarget.transform;
                }
            }

        }

        private static void AddTreeColliders(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            if (m_settings.m_colliderExportBakeCombinedCollisionMesh)
            {
                Mesh builtinCubeMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Cube");
                Mesh builtinCapsuleMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Capsule");
                Mesh builtinSphereMesh = GaiaEditorUtils.LoadAssetFromUniqueAssetPath<Mesh>("Library/unity default resources::Sphere");
                List<CombineInstance> combineInstances = new List<CombineInstance>();
                //Add the existing mesh in as the first instance
                //UnityEngine.Matrix4x4 MCmatrix = UnityEngine.Matrix4x4.TRS(mainMC.transform.position - terrain.transform.position, mainMC.transform.rotation, mainMC.transform.lossyScale);
                //combineInstances.Add(new CombineInstance() { mesh = mainMC.sharedMesh, transform = MCmatrix });

                //Fetch the tree prototype colliders / layers / heights once and store them in a dictonary by index, we don't want to look up the same information again and again for every tree instance
                Dictionary<int, Collider[]> protoTreeColliders = new Dictionary<int, Collider[]>();
                Dictionary<int, float> protoTreeColliderMeshHeightScale = new Dictionary<int, float>();
                Dictionary<int, int> protoTreeLayers = new Dictionary<int, int>();


                for (int i = 0; i < terrain.terrainData.treePrototypes.Length; i++)
                {
                    TreePrototype treeProto = (TreePrototype)terrain.terrainData.treePrototypes[i];
                    //We do need the collider array per prefab in any case to know if to do collisions for that tree instance at all
                    if (treeProto.prefab != null)
                    {
                        protoTreeColliders.Add(i, treeProto.prefab.GetComponentsInChildren<Collider>());
                    }
                    else
                    {
                        //Just add an empty collider array if prefab does not exist
                        protoTreeColliders.Add(i, new Collider[0]);
                    }
                    //Only need to get height info if working with replacement colliders
                    if (m_settings.m_colliderTreeReplacement != null)
                    {
                        float heightScale = 0;
                        if (treeProto.prefab != null)
                        {
                            LODGroup lODGroup = treeProto.prefab.GetComponent<LODGroup>();
                            if (lODGroup != null)
                            {
                                var allLODs = lODGroup.GetLODs();
                                if (allLODs.Length > 0)
                                {
                                    Renderer[] renderer = allLODs[0].renderers;
                                    if (renderer.Length > 0)
                                    {
                                        heightScale = renderer[0].bounds.size.y / m_settings.m_colliderTreeReplacement.bounds.size.y;
                                    }
                                }
                            }
                        }
                        protoTreeColliderMeshHeightScale.Add(i, heightScale);
                    }
                    protoTreeLayers.Add(i, treeProto.prefab.layer);
                }

                for (int layer = 0; layer < 31; layer++)
                {
                    combineInstances.Clear();

                    for (int k = 0; k < terrain.terrainData.treeInstances.Length; k++)
                    {
                        TreeInstance treeInstance = terrain.terrainData.treeInstances[k];
                        if (protoTreeLayers[treeInstance.prototypeIndex] != layer)
                        {
                            continue;
                        }

                        //No native colliders, nothing to bake
                        if (protoTreeColliders[treeInstance.prototypeIndex].Length <= 0)
                        {
                            continue;
                        }


                        Vector3 position = new Vector3(treeInstance.position.x * terrain.terrainData.size.x, treeInstance.position.y * terrain.terrainData.size.y, treeInstance.position.z * terrain.terrainData.size.z);
                        Vector3 originalPosition = position;
                        Quaternion rotation = Quaternion.identity;
                        Vector3 scale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
                        Mesh mesh = null;

                        if (m_settings.m_colliderTreeReplacement != null)
                        {
                            //Easy mode: Just use the collider provided by user
                            mesh = m_settings.m_colliderTreeReplacement;
                            scale = new Vector3(treeInstance.widthScale / 2f, treeInstance.heightScale * protoTreeColliderMeshHeightScale[treeInstance.prototypeIndex], treeInstance.widthScale / 2f);
                            rotation = Quaternion.Euler(0f, treeInstance.rotation * (180f / (float)Math.PI), 0f);
                            UnityEngine.Matrix4x4 matrix = UnityEngine.Matrix4x4.TRS(position, rotation, scale);
                            combineInstances.Add(new CombineInstance() { mesh = mesh, transform = matrix });

                        }
                        else
                        {
                            //Hard mode: Iterate through all child colliders in the prefab and replace them with the Unity primitives
                            Collider[] allChildColliders = protoTreeColliders[treeInstance.prototypeIndex];
                            for (int i = 0; i < allChildColliders.Length; i++)
                            {
                                Type type = allChildColliders[i].GetType();
                                position = new Vector3(treeInstance.position.x * terrain.terrainData.size.x, treeInstance.position.y * terrain.terrainData.size.y, treeInstance.position.z * terrain.terrainData.size.z);
                                originalPosition = position;
                                rotation = Quaternion.identity;
                                scale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
                                if (type == typeof(BoxCollider))
                                {
                                    BoxCollider box = (BoxCollider)allChildColliders[i];
                                    mesh = builtinCubeMesh;
                                    //scale = Vector3.Scale(box.size, box.transform.lossyScale);
                                    scale.y = box.size.y * treeInstance.heightScale;
                                    scale.x = box.size.x * treeInstance.widthScale;
                                    scale.z = box.size.z * treeInstance.widthScale;
                                    //Take the tree instance scale into account while calculating the center position of the collider
                                    position += new Vector3(box.center.x * treeInstance.widthScale, box.center.y * treeInstance.heightScale, box.center.z * treeInstance.widthScale);
                                }
                                if (type == typeof(CapsuleCollider))
                                {
                                    mesh = builtinCapsuleMesh;

                                    CapsuleCollider capsuleCollider = (CapsuleCollider)allChildColliders[i];

                                    float radiusScaleFactor = 0f;
                                    Vector3 sizeScale = capsuleCollider.transform.lossyScale;

                                    for (int axis = 0; axis < 3; ++axis)
                                    {
                                        if (axis != capsuleCollider.direction)
                                            radiusScaleFactor = Mathf.Max(radiusScaleFactor, Mathf.Abs(sizeScale[axis]));
                                    }

                                    for (int axis = 0; axis < 3; ++axis)
                                    {
                                        if (axis != capsuleCollider.direction)
                                            sizeScale[axis] = Mathf.Sign(sizeScale[axis]) * radiusScaleFactor;
                                    }

                                    //Take the tree instance scale into account while calculating scale
                                    scale.y = 0.5f * capsuleCollider.height * Mathf.Abs(sizeScale[capsuleCollider.direction]) * treeInstance.heightScale;
                                    scale.x = 2f * capsuleCollider.radius * radiusScaleFactor * treeInstance.widthScale;
                                    scale.z = 2f * capsuleCollider.radius * radiusScaleFactor * treeInstance.widthScale;
                                    //Take the tree instance scale into account while calculating the center position of the collider
                                    position += new Vector3(capsuleCollider.center.x * treeInstance.widthScale, capsuleCollider.center.y * treeInstance.heightScale, capsuleCollider.center.z * treeInstance.widthScale);

                                }
                                if (type == typeof(SphereCollider))
                                {
                                    SphereCollider sphere = (SphereCollider)allChildColliders[i];
                                    mesh = builtinSphereMesh;
                                    //double up because radius != diameter
                                    scale.y = 2f * sphere.radius * treeInstance.heightScale;
                                    scale.x = 2f * sphere.radius * treeInstance.widthScale;
                                    scale.z = 2f * sphere.radius * treeInstance.widthScale;
                                    position += new Vector3(sphere.center.x * treeInstance.widthScale, sphere.center.y * treeInstance.heightScale, sphere.center.z * treeInstance.widthScale);
                                }

                                //We need to rotate the collider around the center point of the tree according to the tree rotation to get the correct final position / rotation
                                GameObject gameObject = new GameObject();
                                gameObject.transform.position = position;
                                gameObject.transform.RotateAround(originalPosition, Vector3.up, treeInstance.rotation * (180f / (float)Math.PI));
                                position = gameObject.transform.position;
                                rotation *= gameObject.transform.rotation;
                                GameObject.DestroyImmediate(gameObject);

                                //Capsule colliders can have an additional "direction" rotation, we need to apply that after the other rotations, else the result will not be correct.
                                if (type == typeof(CapsuleCollider))
                                {
                                    CapsuleCollider capsuleCollider = (CapsuleCollider)allChildColliders[i];
                                    switch (capsuleCollider.direction)
                                    {
                                        case 0: //X
                                            rotation *= Quaternion.Euler(0f, 0f, 90f);
                                            break;
                                        case 1: //Y
                                                //Nothing to do, the capsule mesh is oriented towards Y already
                                            break;
                                        case 2: //Z
                                            rotation *= Quaternion.Euler(90f, 0f, 0f);
                                            break;
                                    }
                                }
                            }
                            UnityEngine.Matrix4x4 matrix = UnityEngine.Matrix4x4.TRS(position, rotation, scale);
                            combineInstances.Add(new CombineInstance() { mesh = mesh, transform = matrix });
                        }
                    }

                    if (combineInstances.Count > 0)
                    {
                        Mesh combinedMesh = new Mesh();
                        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        combinedMesh.CombineMeshes(combineInstances.ToArray());
                        GameObject layerGO = new GameObject("Trees-" + LayerMask.LayerToName(layer));
                        layerGO.layer = layer;
                        MeshCollider layerCollider = layerGO.AddComponent<MeshCollider>();
                        layerCollider.sharedMesh = combinedMesh;
                        layerCollider.transform.position = gaiaGameObjectCopyTarget.transform.position;
                        layerCollider.transform.parent = gaiaGameObjectCopyTarget.transform;
                    }
                }
            }
            else
            {
                List<GameObject> createdGameObjects = new List<GameObject>();

                foreach (TreeInstance treeInstance in terrain.terrainData.treeInstances)
                {
                    GameObject collGO = new GameObject();
                    collGO.transform.position = terrain.transform.position + (new Vector3(treeInstance.position.x * terrain.terrainData.size.x, treeInstance.position.y * terrain.terrainData.size.y, treeInstance.position.z * terrain.terrainData.size.z));
                    collGO.transform.rotation = Quaternion.Euler(0f, treeInstance.rotation, 0f);
                    collGO.transform.localScale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
                    GameObject protoPrefab = terrain.terrainData.treePrototypes[treeInstance.prototypeIndex].prefab;
                    collGO.name = protoPrefab.name;
                    collGO.tag = protoPrefab.tag;
                    collGO.layer = protoPrefab.layer;
                    foreach (Collider coll in protoPrefab.GetComponentsInChildren<Collider>())
                    {
                        UnityEditorInternal.ComponentUtility.CopyComponent(coll);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(collGO);
                    }
                    createdGameObjects.Add(collGO);
                }

                foreach (GameObject go in createdGameObjects)
                {
                    go.transform.parent = gaiaGameObjectCopyTarget.transform;
                }
            }

        }

        public static void ExportSingleTerrain(Terrain terrain, Scene originalScene, List<ExportTerrainLODSettings> LODSettingsList, int index, int maxTerrainCount, bool isFinalLOD, bool isImpostorScenes)
        {
            ExportTerrainLODSettings LODSettings = LODSettingsList[index];
            //Make sure we have the original scene activated again
            EditorSceneManager.SetActiveScene(originalScene);

            //Make sure the terrain is active, otherwise e.g. orthographic bake etc. will not work
            if (!terrain.gameObject.activeInHierarchy)
            {
                if (terrain.transform.parent != null)
                {
                    terrain.transform.parent.gameObject.SetActive(true);
                }
                terrain.gameObject.SetActive(true);
            }

            m_currentTerrainCount++;
            string objFileName = m_workingExportPath + Path.DirectorySeparatorChar + LODSettings.namePrefix + terrain.name + ".obj";
            if (m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.OBJFileExport && !isImpostorScenes)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "OBJ File Export (Can take some time, please be patient...)", m_currentTerrainCount, maxTerrainCount, true);
                objFileName = ExportToObj(terrain, m_settings.m_terrainExportMask, m_settings.m_terrainExportMaskChannel, m_settings.m_terrainExportInvertMask, LODSettings);
                m_createdFiles.Add(objFileName);
            }

            string splatMapFileName = "";

            if (LODSettings.m_exportSplatmaps && m_settings.m_convertSourceTerrainsAction != ConversionAction.OBJFileExport)
            {
                splatMapFileName = ExportSplatmap(terrain, LODSettings.namePrefix);
                int i = 0;
                //we support up to 32 splatmap files
                while (File.Exists(splatMapFileName + i.ToString() + ".png") && i < 32)
                {
                    AssetDatabase.ImportAsset(splatMapFileName + i.ToString() + ".png");
                    m_createdFiles.Add(splatMapFileName + i.ToString() + ".png");
                    i++;

                }
            }

            string textureFileName = "";
            if (LODSettings.m_exportTextures && m_settings.m_convertSourceTerrainsAction != ConversionAction.OBJFileExport)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Texture Export", m_currentTerrainCount, maxTerrainCount, true);
                textureFileName = ExportTextures(LODSettings, terrain);
            }

            string normalMapFileName = "";
            if (LODSettings.m_exportNormalMaps && m_settings.m_convertSourceTerrainsAction != ConversionAction.OBJFileExport)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Normal map Export", m_currentTerrainCount, maxTerrainCount, true);
                normalMapFileName = ExportNormalMap(LODSettings, terrain);
                m_createdFiles.Add(normalMapFileName);
            }

            Material material = null;
            string matFileName = "";
            if (LODSettings.m_createMaterials && m_settings.m_convertSourceTerrainsAction != ConversionAction.OBJFileExport)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Creating Material", m_currentTerrainCount, maxTerrainCount, true);
                matFileName = CreateMaterial(LODSettings, out material, objFileName, textureFileName, normalMapFileName, isImpostorScenes);
                m_createdFiles.Add(matFileName);
            }

            GameObject gaiaGameObjectCopyTarget = null;

            if ((m_settings.m_convertSourceTerrainsAction != ConversionAction.OBJFileExport) || m_settings.m_createImpostorScenes)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Creating Game Object", m_currentTerrainCount, maxTerrainCount, true);
                gaiaGameObjectCopyTarget = SetupGameObject(LODSettingsList, index, terrain, objFileName, textureFileName, material, isFinalLOD, isImpostorScenes);
            }
            
            if ((m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain && m_settings.m_copyGaiaGameObjects && !isImpostorScenes || m_settings.m_copyGaiaGameObjectsImpostor && isImpostorScenes) && isFinalLOD)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Copying Gaia Spawns", m_currentTerrainCount, maxTerrainCount, true);
                CopyGaiaSpawns(terrain, gaiaGameObjectCopyTarget);
            }


            if ((m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain && m_settings.m_convertTreesToGameObjects && !isImpostorScenes) && isFinalLOD)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Converting Trees to Game Objects", m_currentTerrainCount, maxTerrainCount, true);
                GaiaUtils.ConvertTreesToGameObjects(terrain, gaiaGameObjectCopyTarget.transform);
            }

            if (m_settings.m_colliderExportAddGameObjectColliders && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly && !isImpostorScenes)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Adding Game Object Colliders", m_currentTerrainCount, maxTerrainCount, true);
                AddGameObjectColliders(terrain, gaiaGameObjectCopyTarget);
            }

            if (m_settings.m_colliderExportAddTreeColliders && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly && !isImpostorScenes)
            {
                ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Adding Tree Colliders", m_currentTerrainCount, maxTerrainCount, true);
                AddTreeColliders(terrain, gaiaGameObjectCopyTarget);
            }


            if (isFinalLOD)
            {
                if (isImpostorScenes)
                {
                    ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Creating Impostor Scene", m_currentTerrainCount, maxTerrainCount, true);
                    CreateImpostorScene(terrain, gaiaGameObjectCopyTarget);
                }
                else
                {
                    if (m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly && m_settings.m_colliderExportCreateColliderScenes)
                    {
                        ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Creating Collider Scene", m_currentTerrainCount, maxTerrainCount, true);
                        CreateColliderScene(terrain, gaiaGameObjectCopyTarget);
                    }
                    ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Exporting Terrains", "Handling Source Terrain", m_currentTerrainCount, maxTerrainCount, true);
                    HandleSourceTerrain(terrain, gaiaGameObjectCopyTarget);
                }
            }


            if (m_settings.m_sourceTerrainTreatment != SourceTerrainTreatment.Delete && !m_processedTerrains.Contains(terrain))
            {
                m_processedTerrains.Add(terrain);
            }
        }


        public static void RestoreBackup()
        {
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                List<TerrainScene> allBackupScenes = TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Where(x => !String.IsNullOrEmpty(x.m_backupScenePath)).ToList();
                List<TerrainScene> allRemainingScenes = TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Where(x => String.IsNullOrEmpty(x.m_backupScenePath)).ToList();
                TerrainLoaderManager.Instance.UnloadAll(true);
                int maxSceneCount = allBackupScenes.Count + allRemainingScenes.Count;
                int current = 0;
                foreach (TerrainScene ts in allBackupScenes)
                {
                    try
                    {
                        ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Restoring Backups", "Restoring Source Terrains...", current, maxSceneCount, true, false);
                        Scene targetScene = EditorSceneManager.OpenScene(ts.m_scenePath, OpenSceneMode.Additive);
                        Scene BackupScene = EditorSceneManager.OpenScene(ts.m_backupScenePath, OpenSceneMode.Additive);
                        GameObject[] rootGOs = targetScene.GetRootGameObjects();
                        for (int i = rootGOs.Count() - 1; i >= 0; i--)
                        {
                            UnityEngine.Object.DestroyImmediate(rootGOs[i]);
                        }
                        foreach (GameObject go in BackupScene.GetRootGameObjects())
                        {
                            EditorSceneManager.MoveGameObjectToScene(go, targetScene);
                        }
                        EditorSceneManager.SaveScene(targetScene);
                        EditorSceneManager.CloseScene(BackupScene, true);
                        EditorSceneManager.CloseScene(targetScene, true);
                        ts.m_backupScenePath = "";
                        ts.m_impostorScenePath = "";
                        current++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Error while restoring Terrain Scenes backup: " + ex.Message + ", Stack Trace: " + ex.StackTrace);
                    }
                }
                foreach (TerrainScene ts in allRemainingScenes)
                {
                    try
                    {
                        ProgressBar.Show(ProgressBarPriority.TerrainMeshExport, "Restoring Backups", "Restoring Source Terrains...", current, maxSceneCount, true, false);
                        Scene targetScene = EditorSceneManager.OpenScene(ts.m_scenePath, OpenSceneMode.Additive);
                        GameObject[] rootGOs = targetScene.GetRootGameObjects();
                        for (int i = rootGOs.Count() - 1; i >= 0; i--)
                        {
                            if (rootGOs[i].name == GaiaConstants.SourceTerrainBackupObject)
                            {
                                Transform child = rootGOs[i].transform.GetChild(0);
                                if (child != null)
                                {
                                    child.parent = null;
                                }
                            }
                            //Make sure we do not delete the original terrains
                            //Those could still be in the scene if the user chose to do "nothing" with the source terrains.
                            if (!rootGOs[i].name.StartsWith("Terrain"))
                            {
                                UnityEngine.Object.DestroyImmediate(rootGOs[i]);
                            }
                        }
                        EditorSceneManager.SaveScene(targetScene);
                        EditorSceneManager.CloseScene(targetScene, true);
                        ts.m_backupScenePath = "";
                        ts.m_impostorScenePath = "";
                        current++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Error while restoring Terrain Scenes backup: " + ex.Message + ", Stack Trace: " + ex.StackTrace);
                    }
                }
                ProgressBar.Clear(ProgressBarPriority.TerrainMeshExport);
                TerrainLoaderManager.Instance.SaveStorageData();
                TerrainLoaderManager.Instance.UnloadAll(true);
                TerrainLoaderManager.Instance.RefreshSceneViewLoadingRange();
            }
            else
            {
                List<Terrain> allTerrains = GetAllTerrains();
                ShowTerrains(allTerrains);
                GameObject exportObject = GaiaUtils.GetTerrainExportObject();
                if (exportObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(exportObject);
                }
            }
        }

        public static void ShowTerrains(List<Terrain> terrains)
        {
            foreach (Terrain t in terrains)
            {
                if (t != null)
                {
                    t.gameObject.SetActive(true);
                }
            }

        }

        private static void CreateBackupScene(Terrain terrain)
        {
            string backupScenePath = terrain.gameObject.scene.path.Replace("Terrain", "Backup");
            string originalScenePath = terrain.gameObject.scene.path;
            Scene BackupScene = EditorSceneManager.GetSceneByPath(backupScenePath);
            //Terrain GO needs to be in scene root
            if (terrain.transform.parent != null)
            {
                GameObject parent = terrain.transform.parent.gameObject;
                terrain.transform.parent = null;
                if (parent.name == GaiaConstants.SourceTerrainBackupObject)
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
            if (BackupScene.path == null)
            {
#if GAIA_2023_PRO
                BackupScene = TerrainSceneCreator.CreateBackupScene(terrain.gameObject.scene, terrain.gameObject, SessionManager.m_session);
                EditorSceneManager.SaveScene(BackupScene);
                EditorSceneManager.CloseScene(BackupScene, true);
#endif
            }
            else
            {
                BackupScene = EditorSceneManager.OpenScene(backupScenePath, OpenSceneMode.Additive);
                foreach (GameObject go in BackupScene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                EditorSceneManager.MoveGameObjectToScene(terrain.gameObject, BackupScene);
                EditorSceneManager.SaveScene(BackupScene);
                EditorSceneManager.CloseScene(BackupScene, true);
            }

            Scene originalScene = EditorSceneManager.GetSceneByPath(originalScenePath);
            EditorSceneManager.SaveScene(originalScene);

            TerrainScene ts = TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Find(x => x.m_scenePath == originalScenePath);
            if (ts != null)
            {
                ts.m_backupScenePath = backupScenePath;
                TerrainLoaderManager.Instance.DirtyStorageData();
            }
            else
            {
                Debug.LogError("Could not find a terrain scene entry for terrain " + terrain.name + " Terrain Loading might not function correctly.");
            }
        }


        private static void HandleSourceTerrain(Terrain terrain, GameObject gaiaGameObjectCopyTarget)
        {
            switch (m_settings.m_sourceTerrainTreatment)
            {
                case SourceTerrainTreatment.Deactivate:
                    if (GaiaUtils.HasDynamicLoadedTerrains())
                    {
                        if (terrain.transform.parent == null || terrain.transform.parent.name != GaiaConstants.SourceTerrainBackupObject)
                        {
                            //We need to put the source terrain under a backup container object - otherwise it would be re-activated by terrain loading automatically!
                            GameObject backupObject = new GameObject();
                            backupObject.name = GaiaConstants.SourceTerrainBackupObject;
                            EditorSceneManager.MoveGameObjectToScene(backupObject, terrain.gameObject.scene);
                            backupObject.transform.position = terrain.gameObject.transform.position;
                            terrain.gameObject.transform.parent = backupObject.transform;
                            terrain.gameObject.transform.localPosition = UnityEngine.Vector3.zero;
                        }
                    }
                    terrain.gameObject.SetActive(false);
                    break;
                case SourceTerrainTreatment.StoreInBackupScenes:
                    if (GaiaUtils.HasDynamicLoadedTerrains())
                    {
                        CreateBackupScene(terrain);

                    }
                    else
                    {
                        Debug.LogWarning("Backup Scenes are requested but the scene does not use terrain loading. Source Terrains will be left as they are.");
                    }
                    break;
                case SourceTerrainTreatment.Delete:
                    if (terrain.transform.parent != null || terrain.transform.parent.name == GaiaConstants.SourceTerrainBackupObject)
                    {
                        GameObject.DestroyImmediate(terrain.transform.parent.gameObject);
                    }
                    else
                    {
                        GameObject.DestroyImmediate(terrain.gameObject);
                    }
                    break;
            }
        }

        public static void ExportTerrain(ExportTerrainSettings settings, string workingExportPath)
        {
            m_settings = settings;
            m_workingExportPath = workingExportPath;
            m_createdFiles.Clear();
            m_processedTerrains.Clear();
            m_createdLODParents.Clear();
            m_currentTerrainCount = 0;

            double timeStamp = GaiaUtils.GetUnixTimestamp();

            //There can be issues in unity 2020.2 and higher when doing the orthographic bake on cached terrains. Force a full unload before the export & disable caching to prevent this
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                TerrainLoaderManager.Instance.m_cacheInEditor = false;
                TerrainLoaderManager.Instance.UnloadAll(true);
            }

            //Make sure directory exists
            GaiaDirectories.CreatePathIfDoesNotExist(m_workingExportPath);

            //store current settings in session
            ExportTerrainSettings lastUsedSettings = UnityEngine.Object.Instantiate(m_settings);
            lastUsedSettings.name = "Terrain Export Settings " + System.DateTime.Now.ToShortDateString();
            SessionManager.m_lastUsedTerrainExportSettings = lastUsedSettings;

            if (m_settings.m_createImpostorScenes)
            {
                ExportWithLODSettings(m_settings.m_exportTerrainLODSettingsImpostors, true);
            }

            if (m_settings.m_convertSourceTerrains)
            {
                //If we are doing a collider export, we create a single level LOD settings list on the fly from the collider resolution setting
                //Collider exports only have a single LOD level and this is the only relevant LOD setting when doing collider exports
                if (m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly)
                {
                    var colliderLODSettings = new List<ExportTerrainLODSettings>() { new ExportTerrainLODSettings() {
                    m_addAlphaChannel = AddAlphaChannel.None,
                    m_bakeVertexColors = false,
                    m_createMaterials = false,
                    m_exportNormalMaps = false,
                    m_exportSplatmaps = false,
                    m_exportTextures = false,
                    m_saveResolution = m_settings.m_colliderExportResolution
                } };
                    ExportWithLODSettings(colliderLODSettings, false);
                }
                else
                {
                    ExportWithLODSettings(m_settings.m_exportTerrainLODSettingsSourceTerrains, false);
                }
            }

            // Load the export path as an object
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(m_workingExportPath, typeof(UnityEngine.Object));

            // Select the path & ping it as well
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            TimeSpan duration = TimeSpan.FromMilliseconds(GaiaUtils.GetUnixTimestamp() - timeStamp);

            Debug.Log("Exported " + m_currentTerrainCount.ToString() + " terrains as mesh terrains to the directory " + m_workingExportPath + "\r\nDuration: " + duration.ToString());

            if (m_settings.m_createImpostorScenes)
            {
                //Log Impostor creation in build log
                BuildConfig config = GaiaUtils.GetOrCreateBuildConfig();
                config.AddBuildHistoryEntry(BuildLogCategory.Impostors, SessionManager.gameObject.scene.name, GaiaUtils.GetUnixTimestamp());
            }

            if (m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly)
            {
                //Log Collider baking, in build log
                BuildConfig config = GaiaUtils.GetOrCreateBuildConfig();
                config.AddBuildHistoryEntry(BuildLogCategory.ColliderBaking, SessionManager.gameObject.scene.name, GaiaUtils.GetUnixTimestamp());
            }

            if (m_copyToPath != "")
            {
                foreach (string fileName in m_createdFiles)
                {
                    string sourcePath = (Application.dataPath + fileName).Replace("AssetsAssets", "Assets");
                    string targetPath = m_copyToPath + fileName.Substring(fileName.LastIndexOf(Path.DirectorySeparatorChar));
                    File.Copy(sourcePath, targetPath, true);
                }
                Debug.Log("Copied " + m_createdFiles.Count.ToString() + " files to the final export directory " + m_settings.m_exportPath);
            }

            TerrainLoaderManager.Instance.m_cacheInEditor = m_cachingWasAllowed;
            TerrainLoaderManager.Instance.UpdateImpostorStateInBuildSettings();


            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                if (m_settings.m_createImpostorScenes)
                {
                    foreach (TerrainScene ts in TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes)
                    {
                        ts.RemoveAllReferences();
                    }
                }
                TerrainLoaderManager.Instance.SetLoadingRange(TerrainLoaderManager.Instance.GetLoadingRange(), m_settings.m_impostorRange);
                TerrainLoaderManager.Instance.SaveStorageData();
            }
        }
    }


    /// <summary>
    /// Selects a mode for the export terrain winodw, "Default" is the regular mode with all controls available, the other modes have reduced UI to focus on impostor / server scene creation only.
    /// </summary>
    public enum ExportTerrainWindowUIMode { Default, ImpostorScenes, ServerScene}

    /// <summary>
    /// 
    /// The obj export part of this utility is sourced from http://wiki.unity3d.com/index.php?title=TerrainObjExporter, and adapted largely untouched into 
    /// Gaia as a convenience helper.
    /// 
    /// Authors:
    /// Eric Haines (Eric5h5): original.
    /// Yun Kyu Choi: C# conversion.
    /// Bit Barrel media: progress bar fix.
    /// 
    /// </summary>

    class ExportTerrain : EditorWindow, IPWEditor
    {
        private EditorUtils m_editorUtils;

        [SerializeField]
        private ExportTerrainSettings settings;
        /// <summary>
        /// The current spawner settings
        /// </summary>
        public ExportTerrainSettings m_settings
        {
            get
            {
                if (settings == null)
                {
                    if (SessionManager != null && SessionManager.m_lastUsedTerrainExportSettings != null)
                    {
                        settings = Instantiate(SessionManager.m_lastUsedTerrainExportSettings);
                        settings.name = settings.name.Replace("(Clone)", "").Trim();
                    }
                    else
                    {
                        settings = ScriptableObject.CreateInstance<ExportTerrainSettings>();
                        settings.name = "Terrain Export Settings " + System.DateTime.Now.ToShortDateString();
                        SetImpostorPreset();
                    }
                }
                return settings;
            }
            set
            {
                settings = value;
            }
        }

        private GaiaSessionManager m_sessionManager;
        private GaiaSessionManager SessionManager
        {
            get
            {
                if (m_sessionManager == null)
                {
                    m_sessionManager = GaiaSessionManager.GetSessionManager(false);
                }
                return m_sessionManager;
            }
        }

        private Vector2 m_scrollPosition;
        private ExportTerrainLODSettings m_currentLODSettings;
        private int m_currentLODLevel;
        private List<ExportTerrainLODSettings> m_currentLODSettingsList;
        private List<ExportTerrainSettings> m_allPresets = new List<ExportTerrainSettings>();
        private GaiaSettings m_gaiaSettings;
        private GUIStyle m_exportBox;
        private string m_SaveAndLoadMessage;
        private MessageType m_SaveAndLoadMessageType;
        private bool m_hideTerrainButtonState;
        private bool m_hideMeshButtonState;

        public bool PositionChecked { get; set; }
        public ExportTerrainWindowUIMode m_UIMode = ExportTerrainWindowUIMode.Default;
        internal int m_buildConfigIndex;

        void OnEnable()
        {
            if (m_editorUtils == null)
            {
                // Get editor utils for this
                m_editorUtils = PWApp.GetEditorUtils(this);
            }
            titleContent = m_editorUtils.GetContent("WindowTitle");

            m_allPresets = GaiaUtils.GetOrCreateUserFiles().m_exportTerrainSettings;

            if (m_settings.m_exportTerrainLODSettingsSourceTerrains.Count <= 0)
            {
                SetImpostorLODs();
            }
        }

        private void SetImpostorPreset()
        {
            m_settings.m_convertSourceTerrainsAction = ConversionAction.MeshTerrain;
            m_settings.m_addTerrainCollider = true;
            m_settings.m_terrainColliderType = TerrainColliderType.MeshCollider;
            m_settings.m_copyGaiaGameObjects = false;
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                m_settings.m_sourceTerrainTreatment = SourceTerrainTreatment.Nothing;
            }
            else
            {
                m_settings.m_sourceTerrainTreatment = SourceTerrainTreatment.Deactivate;
            }
            SetImpostorLODs();
        }

        private void SetLowPolyPreset()
        {
            m_settings.m_convertSourceTerrainsAction = ConversionAction.MeshTerrain;
            m_settings.m_addTerrainCollider = true;
            m_settings.m_terrainColliderType = TerrainColliderType.MeshCollider;
            m_settings.m_copyGaiaGameObjects = true;
            m_settings.m_sourceTerrainTreatment = SourceTerrainTreatment.Deactivate;
            SetLowPolyLODs();
        }


        private void SetImpostorLODs()
        {
            //m_settings.m_exportTerrainLODSettings = new List<ExportTerrainLODSettings>();
            //for (int i = 0; i < 4; i++)
            //{
            //    m_settings.m_exportTerrainLODSettings.Add(new ExportTerrainLODSettings());
            //}

            //int lodLevel = 0;
            //foreach (ExportTerrainLODSettings lodSettings in m_settings.m_exportTerrainLODSettings)
            //{
            //    SetLODToImpostorMode(lodSettings, lodLevel++);
            //}
        }

        private void SetLowPolyLODs()
        {
            //m_settings.m_exportTerrainLODSettings = new List<ExportTerrainLODSettings>();
            //for (int i = 0; i < 2; i++)
            //{
            //    m_settings.m_exportTerrainLODSettings.Add(new ExportTerrainLODSettings());
            //}

            //int lodLevel = 0;
            //foreach (ExportTerrainLODSettings lodSettings in m_settings.m_exportTerrainLODSettings)
            //{
            //    SetLODToLowPolyMode(lodSettings, lodLevel++);
            //}
        }

        void OnGUI()
        {
            m_editorUtils.Initialize();
            EditorGUIUtility.labelWidth = 250;
            m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);
            switch (m_UIMode)
            {
                case ExportTerrainWindowUIMode.Default:
                    m_editorUtils.Panel("ExportTerrainPanel", ExportTerrainPanel, true);
                    break;
                case ExportTerrainWindowUIMode.ImpostorScenes:
                    m_editorUtils.Panel("ExportTerrainImpostorPanel", ExportTerrainImpostorPanel, true);
                    break;
                case ExportTerrainWindowUIMode.ServerScene:
                    m_editorUtils.Panel("ExportTerrainServerScenePanel", ExportTerrainServerScenePanel, true);
                    break;
            }
            
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Loads in an export preset after switching to a different preset
        /// </summary>
        public void SwitchPresetIndex()
        {
            //Remember the old foldout state & export path when switching
            bool oldfoldOutState = m_settings.m_customSettingsFoldedOut;
            string oldPath = m_settings.m_exportPath;
            LoadSettings(m_allPresets[m_settings.m_presetIndex], m_settings.m_presetIndex);
            m_settings.m_customSettingsFoldedOut = oldfoldOutState;
            m_settings.m_exportPath = oldPath;
        }

        private void ExportTerrainServerScenePanel(bool helpEnabled)
        {
            GUI.enabled = !ExportTerrainUtility.m_exportRunning;
            bool currentGUIState = GUI.enabled;
            if ((m_hideTerrainButtonState && Terrain.activeTerrains.Count() <= 0) && !GaiaUtils.HasDynamicLoadedTerrains())
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoTerrain"), MessageType.Warning);
            }
            if (string.IsNullOrEmpty(m_settings.m_exportPath))
            {
                m_settings.m_exportPath = GaiaDirectories.GetExportDirectory() + GaiaDirectories.TERRAIN_MESH_EXPORT_DIRECTORY;
            }
            ExportTerrainUtility.m_workingExportPath = m_settings.m_exportPath;
            ExportTerrainUtility.m_copyToPath = "";
            m_settings.m_addTerrainCollider = m_editorUtils.Toggle("AddTerrainCollider", m_settings.m_addTerrainCollider, helpEnabled);
            if (m_settings.m_addTerrainCollider)
            {
                EditorGUI.indentLevel++;
                m_settings.m_terrainColliderType = (TerrainColliderType)m_editorUtils.EnumPopup("TerrainColliderType", m_settings.m_terrainColliderType, helpEnabled);
                if (m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider)
                {
                    m_settings.m_colliderExportResolution = (SaveResolution)m_editorUtils.EnumPopup("ColliderExportResolution", m_settings.m_colliderExportResolution, helpEnabled);
#if GAIA_MESH_PRESENT
                    DrawMeshSimplificationSettings(ref m_settings.m_colliderSimplifyQuality, ref m_settings.m_customSimplificationSettingsFoldedOut, m_settings.m_colliderSimplificationOptions, helpEnabled);
#endif
                }
                EditorGUI.indentLevel--;
            }
            m_settings.m_colliderExportAddTreeColliders = m_editorUtils.Toggle("AddTreeColliders", m_settings.m_colliderExportAddTreeColliders, helpEnabled);
            if (m_settings.m_colliderExportAddTreeColliders)
            {
                EditorGUI.indentLevel++;
                m_settings.m_colliderTreeReplacement = (Mesh)m_editorUtils.ObjectField("TreeColliderReplacement", m_settings.m_colliderTreeReplacement, typeof(Mesh), false, helpEnabled);
                EditorGUI.indentLevel--;
            }
            m_settings.m_colliderExportAddGameObjectColliders = m_editorUtils.Toggle("AddGameObjectColliders", m_settings.m_colliderExportAddGameObjectColliders, helpEnabled);
            m_settings.m_colliderExportBakeCombinedCollisionMesh = m_editorUtils.Toggle("CombineColliderMeshes", m_settings.m_colliderExportBakeCombinedCollisionMesh, helpEnabled);
            Color normalBGColor = GUI.backgroundColor;
            if (m_gaiaSettings == null)
            {
                m_gaiaSettings = GaiaUtils.GetGaiaSettings();
            }
            GUI.backgroundColor = m_gaiaSettings.GetActionButtonColor();

            if (m_editorUtils.Button("CreateServerSceneButton"))
            {
                if (EditorUtility.DisplayDialog("Start Server Scene Creation", "Do you want to start the server scene creation now? This process can take some time and will overwrite the previously created server scene (if any).", "Start Creation", "Cancel"))
                {

                    ExportButton();
                    GaiaManagerEditor.CreateServerScene(m_buildConfigIndex);
                    this.Close();
                }
            }
            GUI.backgroundColor = normalBGColor;

        }

        private void ExportTerrainImpostorPanel(bool helpEnabled)
        {
            GUI.enabled = !ExportTerrainUtility.m_exportRunning;
            bool currentGUIState = GUI.enabled;
            if ((m_hideTerrainButtonState && Terrain.activeTerrains.Count() <= 0) && !GaiaUtils.HasDynamicLoadedTerrains())
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoTerrain"), MessageType.Warning);
            }
            if (string.IsNullOrEmpty(m_settings.m_exportPath))
            {
                m_settings.m_exportPath = GaiaDirectories.GetExportDirectory() + GaiaDirectories.TERRAIN_MESH_EXPORT_DIRECTORY;
            }
            ExportTerrainUtility.m_workingExportPath = m_settings.m_exportPath;
            ExportTerrainUtility.m_copyToPath = "";

            bool canHaveImpostors = false;

#if GAIA_2023_PRO
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                canHaveImpostors = true;
            }
#endif

            if (!canHaveImpostors)
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoImpostorPossibleWarningPanel"), MessageType.Warning);
            }

            GUI.enabled = canHaveImpostors;
            double regularRange = TerrainLoaderManager.Instance.GetLoadingRange();
            if (GaiaUtils.HasImpostorTerrains())
            {
                m_settings.m_impostorRange = TerrainLoaderManager.Instance.GetImpostorLoadingRange();
            }
            else
            {
                if (m_settings.m_impostorRange == 0)
                {
                    m_settings.m_impostorRange = TerrainLoaderManager.GetDefaultLoadingRangeForTilesize(TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainTilesSize) * 3f;
                }
            }
            EditorGUI.indentLevel++;
            EditorGUIUtility.labelWidth -= 15;
            EditorGUI.BeginChangeCheck();
            regularRange = m_editorUtils.DelayedDoubleField("RegularLoadRange", regularRange, helpEnabled);
            m_settings.m_impostorRange = m_editorUtils.DelayedDoubleField("ImpostorLoadRange", m_settings.m_impostorRange, helpEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                TerrainLoaderManager.Instance.SetLoadingRange(regularRange, m_settings.m_impostorRange);
            }
            EditorGUIUtility.labelWidth += 15;
            m_settings.m_addMeshColliderImpostor = m_editorUtils.Toggle("AddMeshCollider", m_settings.m_addMeshColliderImpostor, helpEnabled);
            m_settings.m_copyGaiaGameObjectsImpostor = m_editorUtils.Toggle("CopyGaiaGameObjects", m_settings.m_copyGaiaGameObjectsImpostor, helpEnabled);
            EditorGUI.indentLevel--;

            GUILayout.Space(EditorGUIUtility.singleLineHeight / 2f);
            m_editorUtils.Heading("LODSettingsImpostorScenes");
            int LODLevel = 0;

            foreach (ExportTerrainLODSettings LODSettings in m_settings.m_exportTerrainLODSettingsImpostors)
            {
                m_currentLODSettings = LODSettings;
                m_currentLODLevel = LODLevel;
                m_currentLODSettingsList = m_settings.m_exportTerrainLODSettingsImpostors;
                m_editorUtils.Panel(new GUIContent("LOD Level " + LODLevel.ToString() + " Settings"), DrawLODLevel, false);
                LODLevel++;
            }
            GUILayout.BeginHorizontal();
            if (m_settings.m_exportTerrainLODSettingsImpostors.Count > 1)
            {
                if (m_editorUtils.Button("RemoveLODLevel"))
                {
                    m_settings.m_exportTerrainLODSettingsImpostors.RemoveAt(m_settings.m_exportTerrainLODSettingsImpostors.Count() - 1);
                }
            }
            if (m_editorUtils.Button("AddLODLevel"))
            {
                ExportTerrainLODSettings newSettings = new ExportTerrainLODSettings();
                newSettings.namePrefix = "LOD" + (m_settings.m_exportTerrainLODSettingsImpostors.Count - 1).ToString() + "_";
                newSettings.m_LODGroupScreenRelativeTransitionHeight = Mathf.Max(0.2f * (3 - m_currentLODLevel), 0f);
                m_settings.m_exportTerrainLODSettingsImpostors.Add(newSettings);
            }
            GUILayout.EndHorizontal();
            Color normalBGColor = GUI.backgroundColor;
            if (m_gaiaSettings == null)
            {
                m_gaiaSettings = GaiaUtils.GetGaiaSettings();
            }
            GUI.backgroundColor = m_gaiaSettings.GetActionButtonColor();
            if (m_editorUtils.Button("StartImpostorCreation"))
            {
                if (EditorUtility.DisplayDialog("Start Impostor Creation", "Do you want to start the impostor creation now? This process can take some time and will overwrite any previously created Impostor Terrains.", "Start Creation", "Cancel"))
                {
                    ExportButton();
                    this.Close();
                }
            }
            GUI.backgroundColor = normalBGColor;
        }


        private void ExportTerrainPanel(bool helpEnabled)
        {
            GUI.enabled = !ExportTerrainUtility.m_exportRunning;

            bool currentGUIState = GUI.enabled;
            if ((m_hideTerrainButtonState && Terrain.activeTerrains.Count() <= 0) && !GaiaUtils.HasDynamicLoadedTerrains())
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoTerrain"), MessageType.Warning);
            }


            m_settings.m_exportSelection = (ExportSelection)m_editorUtils.EnumPopup("ExportSelection", m_settings.m_exportSelection, helpEnabled);


            if (string.IsNullOrEmpty(m_settings.m_exportPath))
            {
                m_settings.m_exportPath = GaiaDirectories.GetExportDirectory() + GaiaDirectories.TERRAIN_MESH_EXPORT_DIRECTORY;
            }
            EditorGUILayout.BeginHorizontal();
            m_settings.m_exportPath = m_editorUtils.TextField("ExportDirectory", m_settings.m_exportPath);

            if (m_editorUtils.Button("ExportDirectoryOpen", GUILayout.Width(80)))
            {
                string path = EditorUtility.SaveFolderPanel(m_editorUtils.GetTextValue("ExportDirectoryWindowTitle"), m_settings.m_exportPath, "");
                if (path.Contains(Application.dataPath))
                {
                    m_settings.m_exportPath = GaiaDirectories.GetPathStartingAtAssetsFolder(path);
                }
                else
                {
                    m_settings.m_exportPath = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            //Path needs to be below the "Assets" directory, otherwise Asset Database functionality will not work
            //need to lock GUI and inform user if they chose a directory outside the "Assets" folder
            if (!m_settings.m_exportPath.StartsWith("Assets"))
            {
                ExportTerrainUtility.m_workingExportPath = GaiaDirectories.GetTempExportPath();
                ExportTerrainUtility.m_copyToPath = m_settings.m_exportPath;
                EditorGUILayout.HelpBox(String.Format(m_editorUtils.GetTextValue("ExportDirectoryNotValid"), ExportTerrainUtility.m_workingExportPath), MessageType.Info);
            }
            else
            {
                ExportTerrainUtility.m_workingExportPath = m_settings.m_exportPath;
                ExportTerrainUtility.m_copyToPath = "";
            }

            m_editorUtils.InlineHelp("ExportDirectory", helpEnabled);
            GUILayout.BeginHorizontal();
            m_editorUtils.Label("ExportPreset", GUILayout.Width(EditorGUIUtility.labelWidth));



            int oldPresetIndex = m_settings.m_presetIndex;


            string[] allPresetNames = m_allPresets.Select(x => x.name).Append("Custom").ToArray();
            int[] allPresetIDs = m_allPresets.Select(x => x.name).Append("Custom").Select((x, i) => i).ToArray();

            //if nothing selected yet, initialize with first entry
            if (m_settings.m_presetIndex == -99)
            {
                m_settings.m_presetIndex = 0;
                m_settings.m_lastUsedPresetName = allPresetNames[0];
            }

            //If the name of the preset has changed, this means the order of presets might have changed or the preset itself was changed
            //treat the stored settings as custom setting then
            if (m_settings.m_presetIndex > allPresetNames.Length - 1 || m_settings.m_lastUsedPresetName != allPresetNames[m_settings.m_presetIndex])
            {
                m_settings.m_presetIndex = allPresetIDs.Length - 1;
                m_settings.m_lastUsedPresetName = allPresetNames[m_settings.m_presetIndex];
            }

            m_settings.m_presetIndex = EditorGUILayout.IntPopup(m_settings.m_presetIndex, allPresetNames, allPresetIDs);


            //ExportPreset oldPreset = m_settings.m_newExportPreset;
            //m_settings.m_newExportPreset = (ExportPreset)m_editorUtils.EnumPopup("ExportPreset", m_settings.m_newExportPreset, helpEnabeld);

            if (!m_settings.m_customSettingsFoldedOut)
            {
                if (m_editorUtils.Button("PlusButtonCustomLODSettings", GUILayout.Width(20), GUILayout.Height(18)))
                {
                    m_settings.m_customSettingsFoldedOut = true;
                }
            }
            else
            {
                if (m_editorUtils.Button("MinusButtonCustomLODSettings", GUILayout.Width(20), GUILayout.Height(18)))
                {
                    m_settings.m_customSettingsFoldedOut = false;
                }
            }
            GUILayout.EndHorizontal();

            if (oldPresetIndex != m_settings.m_presetIndex)
            {
                if (m_settings.m_presetIndex < allPresetNames.Length - 1)
                {
                    SwitchPresetIndex();
                    m_settings.m_lastUsedPresetName = allPresetNames[m_settings.m_presetIndex];
                }
                else
                {
                    //user switched to custom setting => fold out the settings
                    m_settings.m_customSettingsFoldedOut = true;
                }
            }

            //If we are using a preset, check if the user changed one of the settings so can switch away from the preset to custom mode
            if (m_settings.m_presetIndex < allPresetNames.Length - 1)
            {
                if (!m_settings.CompareTo(m_allPresets[m_settings.m_presetIndex]))
                {
                    m_settings.m_presetIndex = allPresetNames.Length - 1;
                }

            }

            if (m_exportBox == null || m_exportBox.normal.background == null)
            {
                m_exportBox = new GUIStyle(EditorStyles.helpBox);
                m_exportBox.margin = new RectOffset(0, 0, 0, 0);
                m_exportBox.padding = new RectOffset(3, 3, 3, 3);
            }

            bool canHaveImpostors = false;
            bool canHaveLowPoly = false;

#if GAIA_2023_PRO
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                canHaveImpostors = true;
            }
            canHaveLowPoly = true;
#endif
            if (!canHaveImpostors && m_settings.m_createImpostorScenes)
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoImpostorPossibleWarning"), MessageType.Warning);
            }

            if (!canHaveLowPoly &&
                m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain && m_settings.m_exportTerrainLODSettingsSourceTerrains.Find(x => x.m_materialShader == ExportedTerrainShader.VertexColor) != null)
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoVertexColorPossibleWarning"), MessageType.Warning);
            }

            if (!m_settings.m_convertSourceTerrains && !m_settings.m_createImpostorScenes)
            {
                EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoExportSelected"), MessageType.Warning);
            }

            float buttonWidth = (EditorGUIUtility.currentViewWidth - 28f) / 2f;

            if (m_settings.m_customSettingsFoldedOut)
            {
                GUILayout.Space(5);
                GUILayout.BeginVertical(m_exportBox, GUILayout.MinWidth(200), GUILayout.MaxWidth(1920));
                {
                    GUILayout.BeginHorizontal();
                    m_settings.m_convertSourceTerrains = m_editorUtils.Toggle(m_settings.m_convertSourceTerrains, new GUIContent("", m_editorUtils.GetTooltip("ConvertSourceTerrain")), GUILayout.Width(15f));
                    m_editorUtils.Heading("ConvertSourceTerrain");
                    GUILayout.EndHorizontal();

                    if (m_settings.m_convertSourceTerrains)
                    {

                        m_settings.m_convertSourceTerrainsAction = (ConversionAction)m_editorUtils.EnumPopup("ConvertSourceTerrainAction", m_settings.m_convertSourceTerrainsAction, helpEnabled);
                        EditorGUI.indentLevel++;
                        switch (m_settings.m_convertSourceTerrainsAction)
                        {
                            case ConversionAction.MeshTerrain:
                                m_settings.m_addTerrainCollider = m_editorUtils.Toggle("AddTerrainCollider", m_settings.m_addTerrainCollider, helpEnabled);
                                if (m_settings.m_addTerrainCollider)
                                {
                                    EditorGUI.indentLevel++;
                                    m_settings.m_terrainColliderType = (TerrainColliderType)m_editorUtils.EnumPopup("TerrainColliderType", m_settings.m_terrainColliderType, helpEnabled);
                                    if (m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider)
                                    {
                                        m_settings.m_colliderExportResolution = (SaveResolution)m_editorUtils.EnumPopup("ColliderExportResolution", m_settings.m_colliderExportResolution, helpEnabled);
#if GAIA_MESH_PRESENT
                                        DrawMeshSimplificationSettings(ref m_settings.m_colliderSimplifyQuality, ref m_settings.m_customSimplificationSettingsFoldedOut, m_settings.m_colliderSimplificationOptions, helpEnabled);
#endif
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                m_settings.m_copyGaiaGameObjects = m_editorUtils.Toggle("CopyGaiaGameObjects", m_settings.m_copyGaiaGameObjects, helpEnabled);
                                m_settings.m_convertTreesToGameObjects = m_editorUtils.Toggle("ConvertTreesToGameObjects", m_settings.m_convertTreesToGameObjects, helpEnabled);
                                m_settings.m_sourceTerrainTreatment = (SourceTerrainTreatment)m_editorUtils.EnumPopup("SourceTerrainTreatment", m_settings.m_sourceTerrainTreatment, helpEnabled);
                                if (m_settings.m_sourceTerrainTreatment == SourceTerrainTreatment.StoreInBackupScenes && !GaiaUtils.HasDynamicLoadedTerrains())
                                {
                                    EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("BackupScenesTerrainLoadingOnlyWarning"), MessageType.Warning);
                                }
                                break;
                            case ConversionAction.ColliderOnly:
                                m_settings.m_addTerrainCollider = m_editorUtils.Toggle("AddTerrainCollider", m_settings.m_addTerrainCollider, helpEnabled);
                                if (m_settings.m_addTerrainCollider)
                                {
                                    EditorGUI.indentLevel++;
                                    m_settings.m_terrainColliderType = (TerrainColliderType)m_editorUtils.EnumPopup("TerrainColliderType", m_settings.m_terrainColliderType, helpEnabled);
                                    if (m_settings.m_terrainColliderType == TerrainColliderType.MeshCollider)
                                    {
                                        m_settings.m_colliderExportResolution = (SaveResolution)m_editorUtils.EnumPopup("ColliderExportResolution", m_settings.m_colliderExportResolution, helpEnabled);
#if GAIA_MESH_PRESENT
                                        DrawMeshSimplificationSettings(ref m_settings.m_colliderSimplifyQuality, ref m_settings.m_customSimplificationSettingsFoldedOut, m_settings.m_colliderSimplificationOptions, helpEnabled);
#endif
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                m_settings.m_colliderExportAddTreeColliders = m_editorUtils.Toggle("AddTreeColliders", m_settings.m_colliderExportAddTreeColliders, helpEnabled);
                                if (m_settings.m_colliderExportAddTreeColliders)
                                {
                                    EditorGUI.indentLevel++;
                                    m_settings.m_colliderTreeReplacement = (Mesh)m_editorUtils.ObjectField("TreeColliderReplacement", m_settings.m_colliderTreeReplacement, typeof(Mesh), false, helpEnabled);
                                    EditorGUI.indentLevel--;
                                }
                                m_settings.m_colliderExportAddGameObjectColliders = m_editorUtils.Toggle("AddGameObjectColliders", m_settings.m_colliderExportAddGameObjectColliders, helpEnabled);
                                m_settings.m_colliderExportBakeCombinedCollisionMesh = m_editorUtils.Toggle("CombineColliderMeshes", m_settings.m_colliderExportBakeCombinedCollisionMesh, helpEnabled);
                                if (GaiaUtils.HasDynamicLoadedTerrains())
                                {
                                    m_settings.m_colliderExportCreateColliderScenes = m_editorUtils.Toggle("CreateColliderScenes", m_settings.m_colliderExportCreateColliderScenes, helpEnabled);
                                }
                                m_settings.m_sourceTerrainTreatment = (SourceTerrainTreatment)m_editorUtils.EnumPopup("SourceTerrainTreatment", m_settings.m_sourceTerrainTreatment, helpEnabled);
                                if (m_settings.m_sourceTerrainTreatment == SourceTerrainTreatment.StoreInBackupScenes && !GaiaUtils.HasDynamicLoadedTerrains())
                                {
                                    EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("BackupScenesTerrainLoadingOnlyWarning"), MessageType.Warning);
                                }
                                break;
                            case ConversionAction.OBJFileExport:
                                m_settings.m_saveFormat = (SaveFormat)m_editorUtils.EnumPopup("ExportFormat", m_settings.m_saveFormat, helpEnabled);
                                if (m_settings.m_exportSelection == ExportSelection.SingleTerrainOnly)
                                {
                                    m_settings.m_terrainExportMask = (Texture2D)m_editorUtils.ObjectField("ExportMask", m_settings.m_terrainExportMask, typeof(Texture2D), false, GUILayout.Height(16f));
                                    m_settings.m_terrainExportMaskChannel = (GaiaConstants.ImageChannel)m_editorUtils.EnumPopup("ExportMaskChannel", m_settings.m_terrainExportMaskChannel, helpEnabled);
                                    m_settings.m_terrainExportInvertMask = m_editorUtils.Toggle("ExportMaskInvertChannel", m_settings.m_terrainExportInvertMask, helpEnabled);
                                }
                                break;
                        }
                        EditorGUI.indentLevel--;


                        if (m_settings.m_convertSourceTerrainsAction == ConversionAction.MeshTerrain)
                        {
                            GUILayout.Space(EditorGUIUtility.singleLineHeight / 2f);
                            m_editorUtils.Heading("LODSettingsSourceTerrainConversion");
                            int LODLevel = 0;

                            foreach (ExportTerrainLODSettings LODSettings in m_settings.m_exportTerrainLODSettingsSourceTerrains)
                            {
                                m_currentLODSettings = LODSettings;
                                m_currentLODLevel = LODLevel;
                                m_currentLODSettingsList = m_settings.m_exportTerrainLODSettingsSourceTerrains;
                                m_editorUtils.Panel(new GUIContent("LOD Level " + LODLevel.ToString() + " Settings"), DrawLODLevel, false);
                                LODLevel++;
                            }
                            GUILayout.BeginHorizontal();
                            if (m_settings.m_exportTerrainLODSettingsSourceTerrains.Count > 1)
                            {
                                if (m_editorUtils.Button("RemoveLODLevel"))
                                {
                                    m_settings.m_exportTerrainLODSettingsSourceTerrains.RemoveAt(m_settings.m_exportTerrainLODSettingsSourceTerrains.Count() - 1);
                                }
                            }
                            if (m_editorUtils.Button("AddLODLevel"))
                            {
                                ExportTerrainLODSettings newSettings = new ExportTerrainLODSettings();
                                newSettings.namePrefix = "LOD" + (m_settings.m_exportTerrainLODSettingsSourceTerrains.Count - 1).ToString() + "_";
                                newSettings.m_LODGroupScreenRelativeTransitionHeight = Mathf.Max(0.2f * (3 - m_currentLODLevel), 0f);
                                m_settings.m_exportTerrainLODSettingsSourceTerrains.Add(newSettings);
                            }
                            GUILayout.EndHorizontal();
                        }

                    }



                }
                GUILayout.EndVertical();
                GUILayout.Space(5);




                GUILayout.BeginVertical(m_exportBox, GUILayout.MinWidth(200), GUILayout.MaxWidth(1920));
                {
                    GUILayout.BeginHorizontal();
                    m_settings.m_createImpostorScenes = m_editorUtils.Toggle(m_settings.m_createImpostorScenes, new GUIContent("", m_editorUtils.GetTooltip("CreateImpostorScenes")), GUILayout.Width(15f));
                    m_editorUtils.Heading("CreateImpostorScenes");

                    GUILayout.EndHorizontal();

                    if (m_settings.m_createImpostorScenes)
                    {
                        if (!canHaveImpostors)
                        {
                            EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("NoImpostorPossibleWarningPanel"), MessageType.Warning);
                        }

                        GUI.enabled = canHaveImpostors;
                        double regularRange = TerrainLoaderManager.Instance.GetLoadingRange();
                        if (GaiaUtils.HasImpostorTerrains())
                        {
                            m_settings.m_impostorRange = TerrainLoaderManager.Instance.GetImpostorLoadingRange();
                        }
                        else
                        {
                            if (m_settings.m_impostorRange == 0)
                            {
                                m_settings.m_impostorRange = TerrainLoaderManager.GetDefaultLoadingRangeForTilesize(TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainTilesSize) * 3f;
                            }
                        }
                        EditorGUI.indentLevel++;
                        EditorGUIUtility.labelWidth -= 15; 
                        EditorGUI.BeginChangeCheck();
                        regularRange = m_editorUtils.DelayedDoubleField("RegularLoadRange", regularRange, helpEnabled);
                        m_settings.m_impostorRange = m_editorUtils.DelayedDoubleField("ImpostorLoadRange", m_settings.m_impostorRange, helpEnabled);
                        if (EditorGUI.EndChangeCheck())
                        {
                            TerrainLoaderManager.Instance.SetLoadingRange(regularRange, m_settings.m_impostorRange);
                        }
                        EditorGUIUtility.labelWidth += 15;
                        m_settings.m_addMeshColliderImpostor = m_editorUtils.Toggle("AddMeshCollider", m_settings.m_addMeshColliderImpostor, helpEnabled);
                        m_settings.m_copyGaiaGameObjectsImpostor = m_editorUtils.Toggle("CopyGaiaGameObjects", m_settings.m_copyGaiaGameObjectsImpostor, helpEnabled);
                        EditorGUI.indentLevel--;

                        GUILayout.Space(EditorGUIUtility.singleLineHeight / 2f);
                        m_editorUtils.Heading("LODSettingsImpostorScenes");
                        int LODLevel = 0;

                        foreach (ExportTerrainLODSettings LODSettings in m_settings.m_exportTerrainLODSettingsImpostors)
                        {
                            m_currentLODSettings = LODSettings;
                            m_currentLODLevel = LODLevel;
                            m_currentLODSettingsList = m_settings.m_exportTerrainLODSettingsImpostors;
                            m_editorUtils.Panel(new GUIContent("LOD Level " + LODLevel.ToString() + " Settings"), DrawLODLevel, false);
                            LODLevel++;
                        }
                        GUILayout.BeginHorizontal();
                        if (m_settings.m_exportTerrainLODSettingsImpostors.Count > 1)
                        {
                            if (m_editorUtils.Button("RemoveLODLevel"))
                            {
                                m_settings.m_exportTerrainLODSettingsImpostors.RemoveAt(m_settings.m_exportTerrainLODSettingsImpostors.Count() - 1);
                            }
                        }
                        if (m_editorUtils.Button("AddLODLevel"))
                        {
                            ExportTerrainLODSettings newSettings = new ExportTerrainLODSettings();
                            newSettings.namePrefix = "LOD" + (m_settings.m_exportTerrainLODSettingsImpostors.Count - 1).ToString() + "_";
                            newSettings.m_LODGroupScreenRelativeTransitionHeight = Mathf.Max(0.2f * (3 - m_currentLODLevel), 0f);
                            m_settings.m_exportTerrainLODSettingsImpostors.Add(newSettings);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
                GUI.enabled = currentGUIState;
                if (!String.IsNullOrEmpty(m_SaveAndLoadMessage))
                    EditorGUILayout.HelpBox(m_SaveAndLoadMessage, m_SaveAndLoadMessageType, true);
                GUILayout.BeginHorizontal();
                if (m_editorUtils.Button("SavePreset", GUILayout.Width(buttonWidth)))
                {
                    string dialogPath = GaiaDirectories.GetUserSettingsDirectory();
                    string filename = "Terrain Mesh Export Settings";
                    string saveFilePath = EditorUtility.SaveFilePanel("Save Export settings as..", dialogPath, filename, "asset");

                    bool saveConditionsMet = true;

                    //Do we have a path to begin with?
                    if (saveFilePath == null || saveFilePath == "")
                    {
                        //Silently abort in this case, the user has pressed "Abort" in the File Open Dialog
                        saveConditionsMet = false;
                    }

                    //Look for the Assets Directory
                    if (!saveFilePath.Contains("Assets") && saveConditionsMet)
                    {
                        m_SaveAndLoadMessage = m_editorUtils.GetContent("SaveNoAssetDirectory").text;
                        m_SaveAndLoadMessageType = MessageType.Error;
                        saveConditionsMet = false;
                    }

                    if (saveConditionsMet)
                    {
                        saveFilePath = GaiaDirectories.GetPathStartingAtAssetsFolder(saveFilePath);

                        AssetDatabase.CreateAsset(m_settings, saveFilePath);
                        AssetDatabase.ImportAsset(saveFilePath);

                        //Check if save was successful
                        ExportTerrainSettings settingsToLoad = (ExportTerrainSettings)AssetDatabase.LoadAssetAtPath(saveFilePath, typeof(ExportTerrainSettings));
                        if (settingsToLoad != null)
                        {
                            m_SaveAndLoadMessage = m_editorUtils.GetContent("SaveSuccessful").text;
                            m_SaveAndLoadMessageType = MessageType.Info;
                            EditorGUIUtility.PingObject(settingsToLoad);

                            //Add the saved file to the user file collection so it shows up in the Gaia Manager
                            UserFiles userFiles = GaiaUtils.GetOrCreateUserFiles();
                            if (userFiles.m_autoAddNewFiles)
                            {
                                if (!userFiles.m_exportTerrainSettings.Contains(settingsToLoad))
                                {
                                    userFiles.m_exportTerrainSettings.Add(settingsToLoad);
                                }
                                userFiles.PruneNonExisting();
                            }
                            EditorUtility.SetDirty(userFiles);
                            AssetDatabase.SaveAssets();

                            //dissociate the current settings from the file we just saved, otherwise the user will continue editing the file afterwards
                            //We do this by just instantiating the file we just loaded
                            m_settings = Instantiate(settingsToLoad);
                        }
                        else
                        {
                            m_SaveAndLoadMessage = m_editorUtils.GetContent("SaveFailed").text;
                            m_SaveAndLoadMessageType = MessageType.Error;
                        }
                    }
                }

                if (m_editorUtils.Button("LoadPreset", GUILayout.Width(buttonWidth)))
                {
                    string path = GaiaDirectories.GetUserSettingsDirectory();
                    path = path.Remove(path.LastIndexOf(Path.AltDirectorySeparatorChar));

                    string openFilePath = EditorUtility.OpenFilePanel("Load Export settings..", path, "asset");

                    bool loadConditionsMet = true;

                    //Do we have a path to begin with?
                    if (openFilePath == null || openFilePath == "")
                    {
                        //Silently abort in this case, the user has pressed "Abort" in the File Open Dialog
                        loadConditionsMet = false;
                    }

                    //Look for the Assets Directory
                    if (!openFilePath.Contains("Assets") && loadConditionsMet)
                    {
                        m_SaveAndLoadMessage = m_editorUtils.GetContent("LoadNoAssetDirectory").text;
                        m_SaveAndLoadMessageType = MessageType.Error;
                        loadConditionsMet = false;
                    }
                    if (loadConditionsMet)
                    {
                        openFilePath = GaiaDirectories.GetPathStartingAtAssetsFolder(openFilePath);
                        ExportTerrainSettings settingsToLoad = (ExportTerrainSettings)AssetDatabase.LoadAssetAtPath(openFilePath, typeof(ExportTerrainSettings));

                        if (settingsToLoad != null)
                        {
                            //always switch to Custom setting after loading
                            LoadSettings(settingsToLoad, allPresetIDs.Length - 1);
                            m_SaveAndLoadMessage = m_editorUtils.GetContent("LoadSuccessful").text;
                            m_SaveAndLoadMessageType = MessageType.Info;
                        }
                        else
                        {
                            m_SaveAndLoadMessage = m_editorUtils.GetContent("LoadFailed").text;
                            m_SaveAndLoadMessageType = MessageType.Error;
                        }
                    }
                }
                GUILayout.EndHorizontal();


            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            Color normalBGColor = GUI.backgroundColor;
            if (m_gaiaSettings == null)
            {
                m_gaiaSettings = GaiaUtils.GetGaiaSettings();
            }

            EditorGUILayout.BeginHorizontal();

            //if (allTerrains == null || allTerrains.Count<=0)
            //{
            //    GUI.enabled = false;
            //    m_editorUtils.Button("ShowTerrains", GUILayout.Width(buttonWidth));
            //    GUI.enabled = currentGUIState;
            //}
            //else
            //{
            if (m_hideTerrainButtonState)
            {
                if (m_editorUtils.Button("HideTerrains", GUILayout.Width(buttonWidth)))
                {
                    HideTerrains(ExportTerrainUtility.GetAllTerrains());
                    m_hideTerrainButtonState = !m_hideTerrainButtonState;
                }
            }
            else
            {
                if (m_editorUtils.Button("ShowTerrains", GUILayout.Width(buttonWidth)))
                {
                    ExportTerrainUtility.ShowTerrains(ExportTerrainUtility.GetAllTerrains());
                    m_hideTerrainButtonState = !m_hideTerrainButtonState;

                }
            }
            // }

            //GameObject gaiaExportObject = GaiaUtils.GetTerrainExportObject(false);

            //if (allMeshGameObjects == null || allMeshGameObjects.Count<=0)
            //{
            //    GUI.enabled = false;
            //    m_editorUtils.Button("ShowTerrainExports", GUILayout.Width(buttonWidth));
            //    GUI.enabled = currentGUIState;
            //}
            //else
            //{
            if (m_hideMeshButtonState)
            {
                if (m_editorUtils.Button("HideTerrainExports", GUILayout.Width(buttonWidth)))
                {
                    HideMeshTerrains(GetAllMeshTerrains());
                    m_hideMeshButtonState = !m_hideMeshButtonState;
                }

            }
            else
            {
                if (m_editorUtils.Button("ShowTerrainExports", GUILayout.Width(buttonWidth)))
                {
                    ShowMeshTerrains(GetAllMeshTerrains());
                    m_hideMeshButtonState = !m_hideMeshButtonState;
                }
            }
            //}
            EditorGUILayout.EndHorizontal();

            bool exportEnabled = true;

            if (!GaiaUtils.HasDynamicLoadedTerrains() && m_settings.m_createImpostorScenes)
            {
                exportEnabled = false;
            }


            GUILayout.Space(EditorGUIUtility.singleLineHeight);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = exportEnabled;
            GUI.backgroundColor = m_gaiaSettings.GetActionButtonColor();

            if (m_editorUtils.Button("ExportButton", GUILayout.Width(buttonWidth)))
            {
                ExportButton();
            }
            GUI.backgroundColor = normalBGColor;
            GUI.enabled = currentGUIState;

            if (m_editorUtils.Button("RestoreBackupScenes", GUILayout.Width(buttonWidth)))
            {

                if (GaiaUtils.HasDynamicLoadedTerrains() && TerrainLoaderManager.ColliderOnlyLoadingActive)
                {
                    if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("RestoreRegularLoadingTitle"), m_editorUtils.GetTextValue("RestoreRegularLoadingText"), m_editorUtils.GetTextValue("Continue"), m_editorUtils.GetTextValue("Cancel")))
                    {
                        // do an unload with the old setting
                        TerrainLoaderManager.Instance.UnloadAll(true);
                        //then change the actual flag in storage
                        TerrainLoaderManager.Instance.TerrainSceneStorage.m_colliderOnlyLoading = false;
                        //now do a refresh under the new setting
                        TerrainLoaderManager.Instance.RefreshSceneViewLoadingRange();
                    }
                    else
                    {
                        EditorGUIUtility.ExitGUI();
                        return;
                    }
                }

                if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("RestoreBackupTitle"), m_editorUtils.GetTextValue("RestoreBackupText"), m_editorUtils.GetTextValue("Continue"), m_editorUtils.GetTextValue("Cancel")))
                {
                    ExportTerrainUtility.RestoreBackup();
                }
            }


            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private void ExportButton()
        {
            //remember original caching setting
            ExportTerrainUtility.m_cachingWasAllowed = TerrainLoaderManager.Instance.m_cacheInEditor;

            //Ask for permission to switch back to regular loading if the collider only mode is active
            if (GaiaUtils.HasDynamicLoadedTerrains() && TerrainLoaderManager.ColliderOnlyLoadingActive)
            {
                if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("RestoreRegularLoadingTitle"), m_editorUtils.GetTextValue("RestoreRegularLoadingText"), m_editorUtils.GetTextValue("Continue"), m_editorUtils.GetTextValue("Cancel")))
                {
                    // do an unload with the old setting
                    TerrainLoaderManager.Instance.UnloadAll(true);
                    //then change the actual flag in storage
                    TerrainLoaderManager.Instance.TerrainSceneStorage.m_colliderOnlyLoading = false;
                    //now do a refresh under the new setting
                    TerrainLoaderManager.Instance.RefreshSceneViewLoadingRange();
                }
                else
                {
                    EditorGUIUtility.ExitGUI();
                    return;
                }
            }

            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                if (TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes.Where(x => !String.IsNullOrEmpty(x.m_backupScenePath)).Count() > 0)
                {
                    if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("RestoreBackupExportTitle"), m_editorUtils.GetTextValue("RestoreBackupExportText"), m_editorUtils.GetTextValue("Continue"), m_editorUtils.GetTextValue("Cancel")))
                    {
                        ExportTerrainUtility.RestoreBackup();
                    }
                    else
                    {
                        EditorGUIUtility.ExitGUI();
                        return;
                    }
                }
                else
                {
                    Action<Terrain> act = (t) => RemoveMeshTerrainFromTerrainScene(t);
                    GaiaUtils.CallFunctionOnDynamicLoadedTerrains(act, true, null, "Removing old Mesh Terrains...");

                    TerrainLoaderManager.Instance.UnloadAllImpostors(true);
                    //In general we want to remove all impostor references - we are doing a new export which might not utilize impostors
                    //But for collider scene exports it can be valuable to keep the existing impostor scenes, so that the user can switch back and forth
                    //between collider loading & regular with impostors.
                    if (!(m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly))
                    {
                        foreach (TerrainScene ts in TerrainLoaderManager.Instance.TerrainSceneStorage.m_terrainScenes)
                        {
                            ts.m_impostorScenePath = "";
                        }
                        TerrainLoaderManager.Instance.SaveStorageData();
                    }

                }
            }
            else
            {
                List<GameObject> allMeshGameObjects = GetAllMeshTerrains();
                if (allMeshGameObjects != null && allMeshGameObjects.Count > 0)
                {
                    for (int i = allMeshGameObjects.Count - 1; i >= 0; i--)
                    {
                        DestroyImmediate(allMeshGameObjects[i]);
                    }
                }
            }

            try
            {
                ExportTerrainUtility.ExportTerrain(settings, ExportTerrainUtility.m_workingExportPath);

                if (GaiaUtils.HasDynamicLoadedTerrains() && m_settings.m_convertSourceTerrains && m_settings.m_convertSourceTerrainsAction == ConversionAction.ColliderOnly)
                {
                    if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("CreatedColliderScenesTitle"), m_editorUtils.GetTextValue("CreatedColliderScenesText"), m_editorUtils.GetTextValue("SwitchToCollidersOnly"), m_editorUtils.GetTextValue("StayWithRegularTerrains")))
                    {
                        // do an unload with the old setting
                        TerrainLoaderManager.Instance.UnloadAll(true);
                        //then change the actual flag in storage
                        TerrainLoaderManager.Instance.TerrainSceneStorage.m_colliderOnlyLoading = true;
                        //now do a refresh under the new setting
                        TerrainLoaderManager.Instance.RefreshSceneViewLoadingRange();

                        GaiaSessionManager.AddOnlyColliderScenesToBuildSettings(TerrainLoaderManager.TerrainScenes);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during the terrain mesh export, Message: {ex.Message}, Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                TerrainLoaderManager.Instance.m_cacheInEditor = ExportTerrainUtility.m_cachingWasAllowed;
            }
        }


        /// <summary>
        /// Looks for a keyword in the existing export presets, then applies the first preset with that keyword that is found.
        /// </summary>
        /// <param name="searchString"></param>
        public void FindAndSetPreset(string searchString)
        {
            //Find the export collider preset in the user settings
            var allPresets = GaiaUtils.GetOrCreateUserFiles().m_exportTerrainSettings;
            int foundPresetIndex = -99;
            for (int i = 0; i < allPresets.Count; i++)
            {
                if (allPresets[i].name.Contains(searchString))
                {
                    foundPresetIndex = i;
                    break;
                }
            }
            if (foundPresetIndex != -99)
            {
                m_settings.m_presetIndex = foundPresetIndex;
                m_settings.m_lastUsedPresetName = allPresets[foundPresetIndex].name;
                SwitchPresetIndex();
            }
        }

        private void LoadSettings(ExportTerrainSettings settingsToLoad, int selectedIndex)
        {
            m_settings = Instantiate(settingsToLoad);
            //Remove the "Clone" in the name
            m_settings.name = m_settings.name.Replace("(Clone)", "");
            m_settings.m_lastUsedPresetName = m_settings.name;
            //override the selected Index with what is currently selected - there can be any index stored in the old data which might not be up to date anymore.
            m_settings.m_presetIndex = selectedIndex;
            //If "Backup Scenes" are used in a non terrain loading scenario, fall back to "Deactivate" instead. Backup scenes are only supported for terrain loading!
            if (m_settings.m_sourceTerrainTreatment == SourceTerrainTreatment.StoreInBackupScenes && !GaiaUtils.HasDynamicLoadedTerrains())
            {
                m_settings.m_sourceTerrainTreatment = SourceTerrainTreatment.Deactivate;
            }

        }

        private void RemoveMeshTerrainFromTerrainScene(Terrain terrain)
        {
            //We look for two things to remove: single meshes and Mesh LOD groups (created when the user selects multiple LOD levels for the export)
            string searchString1 = GaiaConstants.MeshTerrainName;
            string searchString2 = GaiaConstants.MeshTerrainLODGroupPrefix;

            GameObject[] rootGOs = terrain.gameObject.scene.GetRootGameObjects();
            for (int i = rootGOs.Count() - 1; i >= 0; i--)
            {
                if (rootGOs[i].name == GaiaConstants.SourceTerrainBackupObject)
                {
                    rootGOs[i].transform.GetChild(0).gameObject.SetActive(true);
                }
                if (rootGOs[i].name.StartsWith(searchString1) || rootGOs[i].name.StartsWith(searchString2))
                {
                    DestroyImmediate(rootGOs[i]);
                }
            }
        }


        private void ChangeMeshActiveStateInTerrainScene(Terrain terrain, bool active)
        {
            //We look for two things: single meshes and Mesh LOD groups (created when the user selects multiple LOD levels for the export)
            string searchString1 = GaiaConstants.MeshTerrainName;
            string searchString2 = GaiaConstants.MeshTerrainLODGroupPrefix;

            GameObject[] rootGOs = terrain.gameObject.scene.GetRootGameObjects();
            for (int i = rootGOs.Count() - 1; i >= 0; i--)
            {
                if (rootGOs[i].name.StartsWith(searchString1) || rootGOs[i].name.StartsWith(searchString2))
                {
                    rootGOs[i].SetActive(active);
                }
            }
        }

        private void DrawLODLevel(bool helpEnabled)
        {
            m_currentLODSettings.namePrefix = "LOD" + m_currentLODLevel.ToString() + "_";
            //Mesh Resolution only displayed for LOD level 0 - following levels will inherit the mesh from the foregoing LOD
            if (m_currentLODLevel == 0)
            {
                m_currentLODSettings.m_saveResolution = (SaveResolution)m_editorUtils.EnumPopup("Resolution", m_currentLODSettings.m_saveResolution, helpEnabled);
            }
#if GAIA_MESH_PRESENT
            DrawMeshSimplificationSettings(ref m_currentLODSettings.m_simplifyQuality, ref m_currentLODSettings.m_customSimplifySettingsFoldedOut, m_currentLODSettings.m_simplificationOptions, helpEnabled);
#endif
            if (m_currentLODSettingsList.Count > 1)
            {

                Color regularBGColor = GUI.backgroundColor;

                bool LODRangeMismatch = false;

                if (m_currentLODLevel - 1 >= 0)
                {
                    if (m_currentLODSettings.m_LODGroupScreenRelativeTransitionHeight > m_currentLODSettingsList[m_currentLODLevel - 1].m_LODGroupScreenRelativeTransitionHeight)
                    {
                        LODRangeMismatch = true;
                    }
                }


                if (LODRangeMismatch)
                {
                    GUI.backgroundColor = Color.red;
                }

                m_currentLODSettings.m_LODGroupScreenRelativeTransitionHeight = m_editorUtils.Slider("LODGroupRange", m_currentLODSettings.m_LODGroupScreenRelativeTransitionHeight * 100f, 100f, 0f, helpEnabled) / 100f;

                GUI.backgroundColor = regularBGColor;

                if (LODRangeMismatch)
                {
                    EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("LODRangeMismatch"), MessageType.Warning);
                }
            }



            GUILayout.BeginHorizontal();
            {
                LODSettingsMode oldMode = m_currentLODSettings.m_LODSettingsMode;
                m_currentLODSettings.m_LODSettingsMode = (LODSettingsMode)m_editorUtils.EnumPopup("LODSettingsMode", m_currentLODSettings.m_LODSettingsMode, helpEnabled);

                if (!m_currentLODSettings.m_settingsFoldedOut)
                {
                    if (m_editorUtils.Button("PlusButtonCustomLODSettings", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        m_currentLODSettings.m_settingsFoldedOut = true;
                    }
                }
                else
                {
                    if (m_editorUtils.Button("MinusButtonCustomLODSettings", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        m_currentLODSettings.m_settingsFoldedOut = false;
                    }
                }

                if (oldMode != m_currentLODSettings.m_LODSettingsMode)
                {
                    if (oldMode != LODSettingsMode.Custom && m_currentLODSettings.m_LODSettingsMode == LODSettingsMode.Custom)
                    {
                        m_currentLODSettings.m_settingsFoldedOut = true;
                    }

                    switch (m_currentLODSettings.m_LODSettingsMode)
                    {
                        case LODSettingsMode.Impostor:
                            ExportTerrainSettings.SetLODToImpostorMode(m_currentLODSettings, m_currentLODLevel);
                            break;
                        case LODSettingsMode.LowPoly:
                            ExportTerrainSettings.SetLODToLowPolyMode(m_currentLODSettings, m_currentLODLevel);
                            break;
                        case LODSettingsMode.Custom:
                            break;
                    }

                }
            }

            //If we are using a preset, check if the user changed one of the settings so can switch away from the preset to custom mode
            if (m_currentLODSettings.m_LODSettingsMode != LODSettingsMode.Custom)
            {
                switch (m_currentLODSettings.m_LODSettingsMode)
                {
                    case LODSettingsMode.Impostor:
                        if (
                        m_currentLODSettings.m_normalEdgeMode != NormalEdgeMode.Smooth ||
                        m_currentLODSettings.m_exportTextures != true ||
                        m_currentLODSettings.m_textureExportMethod != TextureExportMethod.OrthographicBake ||
                        m_currentLODSettings.m_bakeLighting != BakeLighting.NeutralLighting ||
                        m_currentLODSettings.m_captureBaseMapTextures != false ||
                        m_currentLODSettings.m_bakeVertexColors != false ||
                        m_currentLODSettings.m_addAlphaChannel != AddAlphaChannel.None ||
                        m_currentLODSettings.m_exportNormalMaps != true ||
                        m_currentLODSettings.m_exportSplatmaps != false ||
                        m_currentLODSettings.m_createMaterials != true ||
                        m_currentLODSettings.m_materialShader != ExportedTerrainShader.Standard)
                        {
                            m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                        }
                        switch (m_currentLODLevel)
                        {
                            case 0:
                                if (m_currentLODSettings.m_textureExportResolution != TextureExportResolution.x2048)
                                    m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                                break;
                            case 1:
                                if (m_currentLODSettings.m_textureExportResolution != TextureExportResolution.x1024)
                                    m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                                break;
                            case 2:
                                if (m_currentLODSettings.m_textureExportResolution != TextureExportResolution.x512)
                                    m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                                break;
                            default:
                                if (m_currentLODSettings.m_textureExportResolution != TextureExportResolution.x256)
                                    m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                                break;
                        }

                        break;
                    case LODSettingsMode.LowPoly:
                        if (
                        m_currentLODSettings.m_normalEdgeMode != NormalEdgeMode.Sharp ||
                        m_currentLODSettings.m_exportTextures != true ||
                        m_currentLODSettings.m_textureExportMethod != TextureExportMethod.BaseMapExport ||
                        m_currentLODSettings.m_bakeVertexColors != true ||
                        m_currentLODSettings.m_VertexColorSmoothing != 3 ||
                        m_currentLODSettings.m_addAlphaChannel != AddAlphaChannel.None ||
                        m_currentLODSettings.m_exportNormalMaps != false ||
                        m_currentLODSettings.m_exportSplatmaps != false ||
                        m_currentLODSettings.m_createMaterials != true ||
                        m_currentLODSettings.m_materialShader != ExportedTerrainShader.VertexColor
                            )
                            m_currentLODSettings.m_LODSettingsMode = LODSettingsMode.Custom;
                        break;
                }

            }
            GUILayout.EndHorizontal();

            if (m_currentLODSettings.m_settingsFoldedOut)
            {

                m_currentLODSettings.m_normalEdgeMode = (NormalEdgeMode)m_editorUtils.EnumPopup("NormalEdgeMode", m_currentLODSettings.m_normalEdgeMode, helpEnabled);

                m_currentLODSettings.m_exportTextures = m_editorUtils.Toggle("ExportTextures", m_currentLODSettings.m_exportTextures, helpEnabled);

                if (m_currentLODSettings.m_exportTextures)
                {
                    EditorGUI.indentLevel++;
                    m_currentLODSettings.m_textureExportMethod = (TextureExportMethod)m_editorUtils.EnumPopup("TextureExportMethod", m_currentLODSettings.m_textureExportMethod, helpEnabled);
                    m_currentLODSettings.m_addAlphaChannel = (AddAlphaChannel)m_editorUtils.EnumPopup("AddAlphaChannel", m_currentLODSettings.m_addAlphaChannel, helpEnabled);
                    if (m_currentLODSettings.m_textureExportMethod == TextureExportMethod.OrthographicBake)
                    {
                        m_currentLODSettings.m_textureExportResolution = (TextureExportResolution)m_editorUtils.EnumPopup("TextureResolution", m_currentLODSettings.m_textureExportResolution, helpEnabled);
                        m_currentLODSettings.m_bakeLayerMask = GaiaEditorUtils.LayerMaskField(m_editorUtils.GetContent("BakeMask"), m_currentLODSettings.m_bakeLayerMask);
                        m_editorUtils.InlineHelp("BakeMask", helpEnabled);
                        m_currentLODSettings.m_bakeLighting = (BakeLighting)m_editorUtils.EnumPopup("BakeLighting", m_currentLODSettings.m_bakeLighting, helpEnabled);
                        m_currentLODSettings.m_captureBaseMapTextures = m_editorUtils.Toggle("CaptureBasemapTextures", m_currentLODSettings.m_captureBaseMapTextures, helpEnabled);
                    }
                    m_currentLODSettings.m_bakeVertexColors = m_editorUtils.Toggle("BakeVertexColors", m_currentLODSettings.m_bakeVertexColors, helpEnabled);
                    if (m_currentLODSettings.m_bakeVertexColors)
                    {
                        EditorGUI.indentLevel++;
                        m_currentLODSettings.m_VertexColorSmoothing = m_editorUtils.IntSlider("VertexColorSmoothing", m_currentLODSettings.m_VertexColorSmoothing, 0, 10, helpEnabled);
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;

                }
                m_currentLODSettings.m_exportNormalMaps = m_editorUtils.Toggle("ExportNormalMaps", m_currentLODSettings.m_exportNormalMaps, helpEnabled);
                m_currentLODSettings.m_exportSplatmaps = m_editorUtils.Toggle("ExportSplatmaps", m_currentLODSettings.m_exportSplatmaps, helpEnabled);
                m_currentLODSettings.m_createMaterials = m_editorUtils.Toggle("CreateMaterials", m_currentLODSettings.m_createMaterials, helpEnabled);
                if (m_currentLODSettings.m_createMaterials)
                {
                    EditorGUI.indentLevel++;
                    m_currentLODSettings.m_materialShader = (ExportedTerrainShader)m_editorUtils.EnumPopup("MaterialShader", m_currentLODSettings.m_materialShader, helpEnabled);
                    if (m_currentLODSettings.m_materialShader == ExportedTerrainShader.VertexColor && !m_currentLODSettings.m_bakeVertexColors)
                    {
                        EditorGUILayout.HelpBox(m_editorUtils.GetTextValue("VertexColorWithoutBaking"), MessageType.Warning);
                    }
                    if (m_currentLODSettings.m_materialShader == ExportedTerrainShader.Custom)
                    {
                        m_currentLODSettings.m_customShader = (Shader)m_editorUtils.ObjectField("CustomShader", m_currentLODSettings.m_customShader, typeof(Shader), false, true); 
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }
#if GAIA_MESH_PRESENT
        private void DrawMeshSimplificationSettings(ref float simplifyQuality, ref bool customSettingsFoldedOut, UnityMeshSimplifierGaia.SimplificationOptions simplificationOptions, bool helpEnabled)
        {
            EditorGUILayout.BeginHorizontal();
            {
                simplifyQuality = m_editorUtils.Slider("SimplifyQuality", simplifyQuality * 100f, 0, 100, helpEnabled) / 100f;
                if (!customSettingsFoldedOut)
                {
                    if (m_editorUtils.Button("PlusButtonCustomSimplifySettings", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        customSettingsFoldedOut = true;
                    }
                }
                else
                {
                    if (m_editorUtils.Button("MinusButtonCustomSimplifySettings", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        customSettingsFoldedOut = false;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            if (customSettingsFoldedOut)
            {
                bool currentGUIState = GUI.enabled;
                if (simplifyQuality >= 1)
                {
                    GUI.enabled = false;
                }

                EditorGUI.indentLevel++;
                simplificationOptions.PreserveBorderEdges = m_editorUtils.Toggle("SimplifyPreserveBorderEdges", simplificationOptions.PreserveBorderEdges, helpEnabled);
                simplificationOptions.PreserveUVSeamEdges = m_editorUtils.Toggle("SimplifyPreserveUVSeamEdges", simplificationOptions.PreserveUVSeamEdges, helpEnabled);
                simplificationOptions.PreserveUVFoldoverEdges = m_editorUtils.Toggle("SimplifyPreserveUVFoldoverEdges", simplificationOptions.PreserveUVFoldoverEdges, helpEnabled);
                simplificationOptions.PreserveSurfaceCurvature = m_editorUtils.Toggle("SimplifyPreserveSurfaceCurvature", simplificationOptions.PreserveSurfaceCurvature, helpEnabled);
                simplificationOptions.EnableSmartLink = m_editorUtils.Toggle("SimplifyEnableSmartLink", simplificationOptions.EnableSmartLink, helpEnabled);
                if (simplificationOptions.EnableSmartLink)
                {
                    EditorGUI.indentLevel++;
                    simplificationOptions.VertexLinkDistance = m_editorUtils.FloatField("SimplifyVertexLinkDistance", (float)simplificationOptions.VertexLinkDistance, helpEnabled);
                    EditorGUI.indentLevel--;
                }
                simplificationOptions.MaxIterationCount = m_editorUtils.IntField("SimplifyMaxInterationCount", simplificationOptions.MaxIterationCount, helpEnabled);
                simplificationOptions.Agressiveness = m_editorUtils.DoubleField("SimplifyAggressiveness", simplificationOptions.Agressiveness, helpEnabled);
                simplificationOptions.ManualUVComponentCount = m_editorUtils.Toggle("SimplifyManualUVComponentCount", simplificationOptions.ManualUVComponentCount, helpEnabled);
                if (simplificationOptions.ManualUVComponentCount)
                {
                    EditorGUI.indentLevel++;
                    simplificationOptions.UVComponentCount = m_editorUtils.IntSlider("SimplifyManualUVComponentCountValue", 0, 4, simplificationOptions.UVComponentCount, helpEnabled);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                GUI.enabled = currentGUIState;
            }
        }
#endif

        /// <summary>
        /// Gets all Mesh terrain game objects in the scene
        /// </summary>
        /// <returns>List of all mesh terrain game objects</returns>
        private List<GameObject> GetAllMeshTerrains()
        {
            List<GameObject> returnList = new List<GameObject>();
            string searchstring = GaiaConstants.MeshTerrainName;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name.StartsWith(searchstring))
                {
                    returnList.Add(go);
                }
            }
            return returnList;
        }

        private void HideTerrains(List<Terrain> terrains)
        {
            foreach (Terrain t in terrains)
            {
                t.gameObject.SetActive(false);
            }
        }

        private void HideMeshTerrains(List<GameObject> meshTerrainGOs)
        {
            foreach (GameObject go in meshTerrainGOs)
            {
                go.SetActive(false);
            }
        }

        private void ShowMeshTerrains(List<GameObject> meshTerrainGOs)
        {
            foreach (GameObject go in meshTerrainGOs)
            {
                go.SetActive(true);
            }
        }

        private void DeleteMeshTerrains(List<GameObject> meshTerrainGOs)
        {
            if (GaiaUtils.HasDynamicLoadedTerrains())
            {
                if (EditorUtility.DisplayDialog(m_editorUtils.GetTextValue("ApplyToUnloadedTerrainsTitle"), m_editorUtils.GetTextValue("ApplyToUnloadedTerrainsText"), m_editorUtils.GetTextValue("YesAllTerrains"), m_editorUtils.GetTextValue("NoCurrentTerrainsOnly")))
                {
                    Action<Terrain> act = (t) => RemoveMeshTerrainFromTerrainScene(t);
                    GaiaUtils.CallFunctionOnDynamicLoadedTerrains(act, true, null, "Remove Meshes...");
                    return;
                }
            }
            for (int i = meshTerrainGOs.Count - 1; i >= 0; i--)
            {
                GameObject go = meshTerrainGOs[i];
                DestroyImmediate(go);
            }
        }

        //void UpdateProgress()
        //{
        //    if (counter++ == progressUpdateInterval)
        //    {
        //        counter = 0;
        //        EditorUtility.DisplayProgressBar("Saving...", "", Mathf.InverseLerp(0, totalCount, ++tCount));
        //    }
        //}
    }
}