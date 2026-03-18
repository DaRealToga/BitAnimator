// Copyright © 2026 DaRealToga 
// Version 1.0 
using UnityEditor;
using UnityEngine;
using AudioVisualization;
using System.IO;

namespace AudioVisualization.Editor
{
    [CustomEditor(typeof(BitAnimator))]
    public class BitAnimatorEditor : UnityEditor.Editor
    {
        private System.Collections.IEnumerator bakingRoutine;

        public override void OnInspectorGUI()
        {
            BitAnimator bitAnimator = (BitAnimator)target;
            serializedObject.Update();

            EditorGUIUtility.labelWidth = 140;

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 10, 10) };
            GUIStyle sectionHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, margin = new RectOffset(0, 0, 5, 5) };
            GUIStyle boxStyle = new GUIStyle("HelpBox") { padding = new RectOffset(10, 10, 10, 10) };
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 };

            EditorGUILayout.LabelField("Bit Animator Pro", titleStyle);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(new GUIContent("Core Setup", "The base settings required to bake your avatar's animation."), sectionHeader);
            EditorGUILayout.BeginVertical(boxStyle);
            
            if (bitAnimator.audioClip != null)
            {
                string path = AssetDatabase.GetAssetPath(bitAnimator.audioClip);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null && importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    EditorGUILayout.HelpBox("WARNING: Your AudioClip Load Type must be set to 'Decompress On Load' in its import settings, or the baking math will fail!", MessageType.Error);
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioClip"));
            
            if (!bitAnimator.enableLiveMode)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"));
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fftResolution"), new GUIContent("FFT Resolution"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(new GUIContent("Pro Toolbox", "Advanced options for testing or VFX Graph generation."), sectionHeader);
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableLiveMode"));
            
            if (bitAnimator.enableLiveMode)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("liveAudioSource"));
                EditorGUILayout.HelpBox("Live mode active! Use Unity Events in your Effect Slots to drive scripts dynamically. DO NOT USE FOR FINAL VRCHAT UPLOADS.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button(new GUIContent("Bake Audio to VFX Graph Texture (.EXR)", "Generates a texture mapping time and frequency. Perfect for driving GPU particles in Unity's VFX Graph!"), GUILayout.Height(30))) 
                    bitAnimator.BakeVFXTexture();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(new GUIContent("Effect Modules", "The individual animations you want to react to the beat (e.g., BlendShapes, Materials, Particles)."), sectionHeader);
            SerializedProperty slotsProp = serializedObject.FindProperty("recordSlots");
            
            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                SerializedProperty slotProp = slotsProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = slotProp.FindPropertyRelative("name");
                SerializedProperty isEnabledProp = slotProp.FindPropertyRelative("isEnabled");

                EditorGUILayout.BeginVertical(boxStyle);

                EditorGUILayout.BeginHorizontal();
                
                isEnabledProp.boolValue = EditorGUILayout.Toggle(new GUIContent("", "Uncheck to mute this effect. It will be completely ignored when baking your animation."), isEnabledProp.boolValue, GUILayout.Width(15));
                
                GUILayout.Space(12); 
                
                Color oldColor = GUI.contentColor;
                if (!isEnabledProp.boolValue) GUI.contentColor = Color.gray;
                
                slotProp.isExpanded = EditorGUILayout.Foldout(slotProp.isExpanded, nameProp.stringValue, true, foldoutStyle);
                GUI.contentColor = oldColor;
                
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Visualizer", "Opens the Spectrogram to visually select your frequency range."), EditorStyles.miniButtonLeft, GUILayout.Width(75))) 
                    BitAnimatorWindow.ShowWindow(bitAnimator, i);
                    
                if (GUILayout.Button(new GUIContent("Save", "Save this exact effect setup to share with other creators."), EditorStyles.miniButtonMid, GUILayout.Width(50)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Preset", "", nameProp.stringValue + ".json", "json");
                    if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, JsonUtility.ToJson(bitAnimator.recordSlots[i], true));
                }
                
                if (GUILayout.Button(new GUIContent("Load", "Load an effect preset JSON file."), EditorStyles.miniButtonRight, GUILayout.Width(50)))
                {
                    string path = EditorUtility.OpenFilePanel("Load Preset", "", "json");
                    if (!string.IsNullOrEmpty(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), bitAnimator.recordSlots[i]); serializedObject.Update(); }
                }
                EditorGUILayout.EndHorizontal();

                if (slotProp.isExpanded)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.PropertyField(nameProp);
                    EditorGUILayout.Space(10);

                    if (!bitAnimator.enableLiveMode)
                    {
                        EditorGUILayout.LabelField(new GUIContent("1. Target Setup", "Select the mesh or material on your avatar you want to animate."), EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("targetObject"));

                        SerializedProperty targetObjProp = slotProp.FindPropertyRelative("targetObject");
                        if (targetObjProp.objectReferenceValue != null)
                        {
                            GameObject targetObj = (GameObject)targetObjProp.objectReferenceValue;
                            EditorCurveBinding[] bindings = AnimationUtility.GetAnimatableBindings(targetObj, targetObj);
                            
                            if (bindings.Length > 0)
                            {
                                string[] displayOptions = new string[bindings.Length];
                                for (int b = 0; b < bindings.Length; b++) displayOptions[b] = bindings[b].type.Name + "/" + bindings[b].propertyName.Replace('.', '/');
                                
                                SerializedProperty bindingTypeProp = slotProp.FindPropertyRelative("bindingType");
                                SerializedProperty bindingPropProp = slotProp.FindPropertyRelative("bindingProperty");
                                SerializedProperty bindingPathProp = slotProp.FindPropertyRelative("bindingPath");

                                int currentIndex = 0;
                                for (int b = 0; b < bindings.Length; b++) { if (bindings[b].propertyName == bindingPropProp.stringValue && bindings[b].type.AssemblyQualifiedName == bindingTypeProp.stringValue) { currentIndex = b; break; } }

                                int newIndex = EditorGUILayout.Popup(new GUIContent("Animate Property", "Pick the exact Unity value you want to bounce to the beat."), currentIndex, displayOptions);
                                bindingTypeProp.stringValue = bindings[newIndex].type.AssemblyQualifiedName;
                                bindingPropProp.stringValue = bindings[newIndex].propertyName;
                                bindingPathProp.stringValue = BitAnimator.CalculateTransformPath(targetObj.transform, bitAnimator.animatorObject != null ? bitAnimator.animatorObject.transform : bitAnimator.transform);
                            }
                        }
                        EditorGUILayout.Space(10);
                    }

                    EditorGUILayout.LabelField(new GUIContent("2. Beat Sculpting", "Control what part of the song this reacts to and how snappy it is."), EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("startFreq"), new GUIContent("Start (Hz)"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("endFreq"), new GUIContent("End (Hz)"));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("volumeMultiplier"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("threshold"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("responseCurve"));
                    EditorGUILayout.Space(10);

                    EditorGUILayout.LabelField(new GUIContent("3. Output Mapping", "Control the absolute minimum and maximum values of the animation."), EditorStyles.boldLabel);
                    SerializedProperty isColorProp = slotProp.FindPropertyRelative("isColor");
                    EditorGUILayout.PropertyField(isColorProp);
                    
                    if (isColorProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("colorGradient"));
                        if (bitAnimator.enableLiveMode) EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("onLiveColor"));
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("minValue"));
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("maxValue"));
                        EditorGUILayout.EndHorizontal();
                        if (bitAnimator.enableLiveMode) EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("onLiveFloat"));
                    }

                    EditorGUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Delete Module", GUILayout.Width(120))) slotsProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Effect Module", GUILayout.Height(30), GUILayout.Width(160))) slotsProp.arraySize++;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(15);

            if (!bitAnimator.enableLiveMode)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button(new GUIContent("BAKE ALL ANIMATIONS", "Calculates the math and saves the keyframes to your Animation Clip. Do this before uploading to VRChat!"), GUILayout.Height(45)))
                {
                    bitAnimator.StartBaking();
                    bakingRoutine = bitAnimator.ComputeAnimation(bitAnimator.animationClip);
                    EditorApplication.update += BakeUpdate;
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void BakeUpdate()
        {
            if (bakingRoutine != null && !bakingRoutine.MoveNext())
            {
                EditorApplication.update -= BakeUpdate;
                bakingRoutine = null;
            }
        }
    }
}