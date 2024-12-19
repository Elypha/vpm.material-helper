using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using Elypha.Helper;
using Elypha.I18N;

namespace Elypha
{
    public class MaterialBulkSwapper : EditorWindow
    {
        private Vector2 scrollPosition;
        private static PluginLanguage language = PluginLanguage.English;
        private MaterialBulkSwapperI18N i18n = new MaterialBulkSwapperI18N(language);
        private Func<string, string> _t;


        private GUIStyle LabelStyleCentered => new(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        private readonly GUILayoutOption[] expanded = new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.MinWidth(300),
        };


        private GameObject avatarObject;
        private GameObject outfitObject;
        private int _lastOutfitObject = -1;
        private AnimationClip animationClip;
        private int _lastAnimationClip = -1;
        private int targetFrameNumber;
        private readonly Dictionary<GameObject, RendererConfig> rendererConfigs = new();
        private readonly Dictionary<Material, MaterialConfig> materialConfigs = new();
        private int clipNextFrame;
        private float targetFrameTime;


        private readonly string[] actionTabs = new string[] { "Replace In-Place", "Create Animation" };
        private int actionTabIndex = 0;
        private bool showAdvancedSettings = false;


        [MenuItem("Tools/Elypha Toolkit/Material Bulk Swapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialBulkSwapper>("Material Bulk Swapper");
            window.minSize = new Vector2(400, 400);

            window.i18n = new MaterialBulkSwapperI18N(language);
            window._t = window.i18n._t;
        }


        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinWidth(400));

            // Settings
            // --------------------------------
            showAdvancedSettings = EditorGUILayout.Foldout(
                showAdvancedSettings,
                "Advanced Settings",
                true,
                new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                }
            );

            if (showAdvancedSettings)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Language", GUILayout.Width(100));
                language = (PluginLanguage)EditorGUILayout.EnumPopup(language, GUILayout.Width(200));
                if (language != i18n.language)
                {
                    i18n = new MaterialBulkSwapperI18N(language);
                    _t = i18n._t;
                }
                GUILayout.EndHorizontal();
            }

            UnityHelper.LabelBoldColored("# Settings", UnityHelper.ColourTitle);
            UnityHelper.Separator(Color.grey, 1, 0, 4);




            // Avatar Object
            avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar Object", avatarObject, typeof(GameObject), true, expanded);

            // Outfit Object
            outfitObject = (GameObject)EditorGUILayout.ObjectField("Outfit Object", outfitObject, typeof(GameObject), true, expanded);
            var _currentOutfitObject = outfitObject == null ? -1 : outfitObject.GetInstanceID();
            if (_lastOutfitObject != _currentOutfitObject)
            {
                _lastOutfitObject = outfitObject.GetInstanceID();
                LoadRenderers();
                LoadMaterials();
            }

            // Reload
            if (GUILayout.Button("Reload"))
            {
                LoadRenderers();
                LoadMaterials();
            }


            GUILayout.Space(8);
            UnityHelper.LabelBoldColored("# Edit", UnityHelper.ColourTitle);
            UnityHelper.Separator(Color.grey, 1, 0, 4);

            // Renderers
            // --------------------------------
            GUILayout.BeginHorizontal();
            UnityHelper.LabelBoldColored("Skinned Mesh Renderers", UnityHelper.ColourBold);
            if (GUILayout.Button("Select All"))
            {
                foreach (var pair in rendererConfigs)
                {
                    pair.Value.Enabled = true;
                }
            }
            if (GUILayout.Button("Select None"))
            {
                foreach (var pair in rendererConfigs)
                {
                    pair.Value.Enabled = false;
                }
            }
            GUILayout.EndHorizontal();

            // Renderer List
            if (rendererConfigs.Count > 0)
            {
                foreach (GameObject go in rendererConfigs.Keys.OrderBy(go => go.name))
                {
                    GUILayout.BeginHorizontal();
                    var currentState = rendererConfigs[go].Enabled;
                    rendererConfigs[go].Enabled = EditorGUILayout.Toggle(rendererConfigs[go].Enabled, GUILayout.Width(16));
                    if (currentState != rendererConfigs[go].Enabled)
                    {
                        LoadMaterials();
                    }
                    EditorGUILayout.ObjectField(go, typeof(GameObject), false);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.BeginHorizontal();
                EditorGUILayout.Toggle(false, GUILayout.Width(16));
                EditorGUILayout.ObjectField(null, typeof(GameObject), false);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }


            // Materials
            // --------------------------------
            GUILayout.BeginHorizontal();
            UnityHelper.LabelBoldColored("Unique Materials", UnityHelper.ColourBold);
            if (GUILayout.Button("Select All"))
            {
                foreach (var pair in materialConfigs)
                {
                    pair.Value.Enabled = true;
                }
            }
            if (GUILayout.Button("Select None"))
            {
                foreach (var pair in materialConfigs)
                {
                    pair.Value.Enabled = false;
                }
            }
            GUILayout.EndHorizontal();

            // Material List
            if (materialConfigs.Count > 0)
            {
                foreach (var pair in materialConfigs.OrderBy(pair => pair.Value.Excluded).ThenBy(pair => pair.Key.name))
                {
                    var mat = pair.Key;
                    if (pair.Value.Excluded) GUI.enabled = false;
                    GUILayout.BeginHorizontal();
                    materialConfigs[mat].Enabled = EditorGUILayout.Toggle(materialConfigs[mat].Enabled, GUILayout.Width(16));
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    EditorGUILayout.LabelField("->", LabelStyleCentered, GUILayout.Width(16));
                    materialConfigs[mat].TargetMaterial = (Material)EditorGUILayout.ObjectField(materialConfigs[mat].TargetMaterial, typeof(Material), false);
                    GUILayout.EndHorizontal();
                    if (pair.Value.Excluded) GUI.enabled = true;
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.BeginHorizontal();
                EditorGUILayout.Toggle(false, GUILayout.Width(16));
                EditorGUILayout.ObjectField(null, typeof(Material), false);
                EditorGUILayout.LabelField("->", LabelStyleCentered, GUILayout.Width(25));
                EditorGUILayout.ObjectField(null, typeof(Material), false);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }


            GUILayout.Space(8);
            UnityHelper.LabelBoldColored("# Select Action", UnityHelper.ColourTitle);
            UnityHelper.Separator(Color.grey, 1, 0, 4);

            // Actions
            // --------------------------------
            GUILayout.BeginVertical();
            actionTabIndex = GUILayout.Toolbar(actionTabIndex, actionTabs.Select(_t).ToArray());
            GUILayout.EndVertical();

            switch (actionTabIndex)
            {
                case 0:
                    DrawReplaceTab();
                    break;
                case 1:
                    DrawCreateAnimationTab();
                    break;
                default:
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReplaceTab()
        {
            UnityHelper.LabelBoldColored("Replace Material", UnityHelper.ColourBold);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restore"))
            {
                ApplyInPlaceMaterialOriginal();
            }
            if (GUILayout.Button("Replace Material"))
            {
                ApplyInPlaceMaterialSwapped();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCreateAnimationTab()
        {
            UnityHelper.LabelBoldColored("Select Swap Animation", UnityHelper.ColourBold);

            // Animation Clip
            GUILayout.BeginHorizontal();
            animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false, expanded);
            var _currentAnimationClip = animationClip == null ? -1 : animationClip.GetInstanceID();
            if (_lastAnimationClip != _currentAnimationClip)
            {
                _lastAnimationClip = _currentAnimationClip;
                LoadAnimationClip();
            }
            if (GUILayout.Button("Reload"))
            {
                LoadAnimationClip();
            }
            GUILayout.EndHorizontal();

            if (animationClip == null) GUI.enabled = false;
            EditorGUILayout.IntField("┗ Next frame on this clip", clipNextFrame);
            if (animationClip == null) GUI.enabled = true;


            // Next Frame
            UnityHelper.LabelBoldColored("Add keyframe", UnityHelper.ColourBold);

            if (animationClip == null) GUI.enabled = false;
            targetFrameNumber = EditorGUILayout.IntField("Target Frame", targetFrameNumber);
            if (animationClip == null) GUI.enabled = true;

            if (animationClip == null)
            {
                GUI.enabled = false;
                GUILayout.Button($"Please select an Animation Clip");
                GUI.enabled = true;
            }
            else
            {
                var seconds = Math.Floor(targetFrameNumber / animationClip.frameRate);
                var frames = targetFrameNumber % animationClip.frameRate;
                targetFrameTime = targetFrameNumber / animationClip.frameRate;
                if (GUILayout.Button($"Add to Frame #{targetFrameNumber} [{seconds}:{frames:00}] (at {targetFrameTime:0.000}s)"))
                {
                    AddMaterialSwapKeyframes();
                }
            }
        }


        private void LoadRenderers()
        {
            UnityHelper.Unfocus();
            if (outfitObject == null) return;

            rendererConfigs.Clear();

            var renderers = UnityHelper.GetSkinnedGameObjects(outfitObject);
            foreach (GameObject go in renderers)
            {
                rendererConfigs[go] = new RendererConfig
                {
                    OriginalMaterials = go.GetComponent<SkinnedMeshRenderer>().sharedMaterials,
                };
            }
        }

        private void LoadMaterials()
        {
            UnityHelper.Unfocus();
            if (outfitObject == null) return;

            materialConfigs.Clear();
            var renderers = GetRenderers();
            var enabledRenderers = GetEnabledRenderers();
            foreach (SkinnedMeshRenderer renderer in enabledRenderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && !materialConfigs.ContainsKey(mat))
                    {
                        materialConfigs[mat] = new MaterialConfig
                        {
                            TargetMaterial = mat
                        };
                    }
                }
            }
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && !materialConfigs.ContainsKey(mat))
                    {
                        materialConfigs[mat] = new MaterialConfig
                        {
                            TargetMaterial = null,
                            Enabled = false,
                            Excluded = true
                        };
                    }
                }
            }
        }

        private void LoadAnimationClip()
        {
            UnityHelper.Unfocus();
            if (animationClip == null) return;

            clipNextFrame = animationClip.empty ? 0 : (int)(animationClip.length * animationClip.frameRate);
            targetFrameNumber = clipNextFrame;
        }



        private void AddMaterialSwapKeyframes()
        {
            UnityHelper.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var path = UnityHelper.GetRelativePathInHierarchy(avatarObject.transform, renderer.transform);

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var mat = renderer.sharedMaterials[i];

                    if (!materialConfigs.ContainsKey(mat) || !materialConfigs[mat].Enabled)
                    {
                        continue;
                    }

                    var binding = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{i}]");
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);

                    var newKeyframe = new ObjectReferenceKeyframe
                    {
                        time = targetFrameTime,
                        value = materialConfigs[mat].TargetMaterial
                    };

                    if (keyframes != null && keyframes.Length > 0)
                    {
                        bool isUpdated = false;
                        for (int j = 0; j < keyframes.Length; j++)
                        {
                            if (Mathf.Approximately(keyframes[j].time, newKeyframe.time))
                            {
                                keyframes[j].value = materialConfigs[mat].TargetMaterial;
                                isUpdated = true;
                                break;
                            }
                        }
                        if (!isUpdated)
                        {
                            keyframes = keyframes.Append(newKeyframe).ToArray();
                        }
                    }
                    else
                    {
                        keyframes = new ObjectReferenceKeyframe[] { newKeyframe };
                    }

                    AnimationUtility.SetObjectReferenceCurve(animationClip, binding, keyframes);
                }
            }

            AssetDatabase.SaveAssets();
            LoadAnimationClip();
        }

        private void ApplyInPlaceMaterialOriginal()
        {
            UnityHelper.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var originalMat = rendererConfigs[renderer.gameObject].OriginalMaterials[i];
                    if (materials[i] != originalMat)
                    {
                        materials[i] = originalMat;
                    }
                }
                renderer.sharedMaterials = materials;
                Undo.RecordObject(renderer, "Change Material");
                EditorUtility.SetDirty(renderer);
            }
        }

        private void ApplyInPlaceMaterialSwapped()
        {
            UnityHelper.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var originalMat = rendererConfigs[renderer.gameObject].OriginalMaterials[i];
                    if (materialConfigs.ContainsKey(originalMat) && materialConfigs[originalMat].Enabled)
                    {
                        materials[i] = materialConfigs[originalMat].TargetMaterial;
                    }
                }
                renderer.sharedMaterials = materials;
                Undo.RecordObject(renderer, "Change Material");
                EditorUtility.SetDirty(renderer);
            }
        }



        // data
        // --------------------------------
        private class RendererConfig
        {
            public bool Enabled = true;
            public Material[] OriginalMaterials;
        }

        private class MaterialConfig
        {
            public Material TargetMaterial;
            public bool Enabled = true;
            public bool Excluded = false;
        }

        private SkinnedMeshRenderer[] GetEnabledRenderers() => rendererConfigs.Where(pair => pair.Value.Enabled).Select(pair => pair.Key.GetComponent<SkinnedMeshRenderer>()).ToArray();
        private SkinnedMeshRenderer[] GetRenderers() => rendererConfigs.Keys.Select(go => go.GetComponent<SkinnedMeshRenderer>()).ToArray();
    }

}