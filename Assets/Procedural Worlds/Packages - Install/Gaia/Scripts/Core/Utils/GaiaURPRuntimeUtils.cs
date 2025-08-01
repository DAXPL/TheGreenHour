﻿using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

#if UPPipeline
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using Gaia.Pipeline;

namespace Gaia.Pipeline.URP
{
    public static class GaiaURPRuntimeUtils
    {
        /// <summary>
        /// Configures reflections to LWRP
        /// </summary>
        public static void ConfigureReflectionProbes()
        {
            ReflectionProbe[] reflectionProbes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            if (reflectionProbes != null)
            {
                foreach(ReflectionProbe probe in reflectionProbes)
                {
                    if (probe.resolution > 512)
                    {
                        Debug.Log(probe.name + " This probes resolution is quite high and could cause performance issues in Lightweight Pipeline. Recommend lowing the resolution if you're targeting mobile platform");
                    }
                }
            }
        }
        /// <summary>
        /// Configures and setup the terrain
        /// </summary>
        /// <param name="profile"></param>
        public static void ConfigureTerrain(UnityPipelineProfile profile)
        {
            TerrainHelper.SetTerrainMaterial(profile.m_universalTerrainMaterial);
        }

#if UPPipeline
        /// <summary>
        /// Sets the shadow distance in URP
        /// </summary>
        /// <param name="pipelineAsset"></param>
        /// <param name="profileValues"></param>
        public static void SetShadowDistance(UniversalRenderPipelineAsset pipelineAsset, GaiaLightingProfileValues profileValues)
        {
            if (pipelineAsset == null || profileValues == null)
            {
                return;
            }

            pipelineAsset.shadowDistance = profileValues.m_shadowDistance;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(pipelineAsset);
#endif
        }
        /// <summary>
        /// Gets or creates UP camera data
        /// </summary>
        /// <param name="camera"></param>
        /// <returns></returns>
        public static UniversalAdditionalCameraData GetUPCameraData(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            UniversalAdditionalCameraData cameraData = camera.gameObject.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            return cameraData;
        }
        /// <summary>
        /// Clears URP camera data in the scene
        /// </summary>
        /// <param name="cameraDatas"></param>
        public static void ClearUPCameraData(UniversalAdditionalCameraData[] cameraDatas)
        {
            if (cameraDatas.Length < 1)
            {
                return;
            }

            foreach (var data in cameraDatas)
            {
                GameObject.DestroyImmediate(data);
            }
        }
        /// <summary>
        /// Gets or creates UP Light data
        /// </summary>
        /// <param name="light"></param>
        /// <returns></returns>
        public static UniversalAdditionalLightData GetUPLightData(Light light)
        {
            if (light == null)
            {
                return null;
            }

            UniversalAdditionalLightData lightData = light.GetComponent<UniversalAdditionalLightData>();
            if (lightData == null)
            {
                lightData = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            }

            return lightData;
        }
        /// <summary>
        /// Clears URP light data in the scene
        /// </summary>
        /// <param name="lightDatas"></param>
        public static void ClearUPLightData(UniversalAdditionalLightData[] lightDatas)
        {
            if (lightDatas.Length < 1)
            {
                return;
            }

            foreach (var data in lightDatas)
            {
                GameObject.DestroyImmediate(data);
            }
        }

        public static RenderingMode GetRenderingPath() 
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null) 
            {
                var renderer = pipeline.GetRenderer(0);
                if (renderer != null) 
                    {
                       // Use reflection to access the private field "m_RenderingMode" in UniversalRenderer
                       var renderingModeField = renderer.GetType().GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (renderingModeField != null) {
                            var renderingMode = (RenderingMode)renderingModeField.GetValue(renderer);
                            return renderingMode;
                        }
                        else 
                        {
                            Debug.LogError("Unable to access rendering mode field.");
                        }
                }
                else 
                {
                    Debug.LogError("Unable to retrieve Scriptable Renderer when trying to get the rendering path.");
                }
            }
            else 
            {
                Debug.LogError("Error when trying to get the rendering path: Current render pipeline is not URP.");
            }
            return RenderingMode.Deferred;
        }

        public static bool SupportsOpaqueTexture()
        {
            // Get the current pipeline asset
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (urpAsset == null)
            {
                Debug.LogError("Current Render Pipeline Asset is not using the Universal Render Pipeline.");
                return false;
            }

            // Get the opaque texture setting
            return urpAsset.supportsCameraOpaqueTexture;
        }
#endif
    }
}