using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Recorded.Editor
{
    public static class ViewmodelSetup
    {
        public const string LayerName = "Viewmodel";

        public static void Configure(GameObject player, Transform arms)
        {
            if (player == null || arms == null || !arms.IsChildOf(player.transform))
                throw new ArgumentException("Choose the player's existing arms hierarchy.");
            var look = player.GetComponentInChildren<FirstPersonLook>();
            var world = look != null ? look.GetComponentInChildren<Camera>() : null;
            if (world == null) throw new InvalidOperationException("Player has no first-person camera.");
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tags.FindProperty("layers");
                for (int i = 8; i < layers.arraySize; i++)
                {
                    if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                    layers.GetArrayElementAtIndex(i).stringValue = LayerName;
                    tags.ApplyModifiedProperties();
                    layer = i;
                    break;
                }
                if (layer < 0) throw new InvalidOperationException("No free user layer for the viewmodel.");
            }
            int mask = 1 << layer;
            var holder = world.transform.Find("Viewmodel");
            if (holder == null)
            {
                holder = new GameObject("Viewmodel").transform;
                holder.SetParent(world.transform, false);
            }
            if (arms.parent != holder) arms.SetParent(holder, true);
            foreach (var node in arms.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = layer;
            foreach (var renderer in arms.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var overlayTransform = world.transform.Find("ViewmodelCamera");
            if (overlayTransform == null)
            {
                overlayTransform = new GameObject("ViewmodelCamera", typeof(Camera)).transform;
                overlayTransform.SetParent(world.transform, false);
            }
            var overlay = overlayTransform.GetComponent<Camera>();
            overlay.fieldOfView = world.fieldOfView;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 10f;
            overlay.cullingMask = mask;
            overlay.clearFlags = CameraClearFlags.Depth;
            overlay.useOcclusionCulling = false;
            var overlayData = overlay.GetComponent<UniversalAdditionalCameraData>();
            if (overlayData == null) overlayData = overlay.gameObject.AddComponent<UniversalAdditionalCameraData>();
            overlayData.renderType = CameraRenderType.Overlay;
            // Render the hands over world depth; weapon hit detection belongs to the future combat system.
            var overlaySerialized = new SerializedObject(overlayData);
            overlaySerialized.FindProperty("m_ClearDepth").boolValue = true;
            var volumeUpdate = overlaySerialized.FindProperty("m_VolumeFrameworkUpdateModeOption");
            volumeUpdate.enumValueIndex = Array.IndexOf(volumeUpdate.enumNames, "ViaScripting");
            overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
            overlayData.volumeLayerMask = 0;
            overlayData.renderPostProcessing = false;
            overlayData.renderShadows = false;
            overlayData.requiresDepthOption = CameraOverrideOption.Off;
            overlayData.requiresColorOption = CameraOverrideOption.Off;
            world.cullingMask &= ~mask;
            var worldData = world.GetComponent<UniversalAdditionalCameraData>();
            if (worldData == null) worldData = world.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (!worldData.cameraStack.Contains(overlay)) worldData.cameraStack.Add(overlay);
            var controller = holder.GetComponent<FirstPersonViewmodel>();
            if (controller == null) controller = holder.gameObject.AddComponent<FirstPersonViewmodel>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("worldCamera").objectReferenceValue = world;
            serialized.FindProperty("viewmodelCamera").objectReferenceValue = overlay;
            serialized.FindProperty("wallMask").intValue = Physics.DefaultRaycastLayers & ~mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(worldData);
            Debug.Assert(worldData.cameraStack.Contains(overlay) && overlay.cullingMask == mask
                && (world.cullingMask & mask) == 0 && overlayData.clearDepth, "Viewmodel camera setup is incomplete.");
        }
    }
}
