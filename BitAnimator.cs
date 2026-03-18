// Copyright © 2026 DaRealToga 
// Version 1.0 
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization
{
    [BurstCompile(CompileSynchronously = true)]
    public struct BurstFFTJob : IJob
    {
        [ReadOnly] public NativeArray<float> InputSamples;
        [WriteOnly] public NativeArray<float> OutputSpectrum;
        public int LogSize;

        public void Execute()
        {
            int n = 1 << LogSize;
            NativeArray<float2> complexData = new NativeArray<float2>(n, Allocator.Temp);

            for (int i = 0; i < n; i++)
            {
                float multiplier = 0.5f * (1f - math.cos(2f * math.PI * i / (n - 1)));
                complexData[i] = new float2(InputSamples[i] * multiplier, 0f);
            }

            int shift = 32 - LogSize;
            for (int i = 0; i < n; i++)
            {
                int j = (int)(math.reversebits((uint)i) >> shift);
                if (j > i) { float2 temp = complexData[i]; complexData[i] = complexData[j]; complexData[j] = temp; }
            }

            for (int s = 1; s <= LogSize; s++)
            {
                int m = 1 << s; int m2 = m >> 1; float theta = -2f * math.PI / m;
                float2 w_m = new float2(math.cos(theta), math.sin(theta));

                for (int k = 0; k < n; k += m)
                {
                    float2 w = new float2(1f, 0f);
                    for (int j = 0; j < m2; j++)
                    {
                        float2 t = complexData[k + j + m2]; float2 u = complexData[k + j];
                        float2 tw = new float2(w.x * t.x - w.y * t.y, w.x * t.y + w.y * t.x);
                        complexData[k + j] = u + tw; complexData[k + j + m2] = u - tw;
                        w = new float2(w.x * w_m.x - w.y * w_m.y, w.x * w_m.y + w.y * w_m.x);
                    }
                }
            }
            for (int i = 0; i < n / 2; i++) { float2 c = complexData[i]; OutputSpectrum[i] = math.sqrt(c.x * c.x + c.y * c.y) / n; }
            complexData.Dispose();
        }
    }

    public class BitAnimator : MonoBehaviour
    {
        [Header("Core Setup")]
        [Tooltip("The Root of your VRChat Avatar (the object with the Animator component).")]
        public Animator animatorObject;
        
        [Tooltip("The song or audio track you want your avatar to react to.")]
        public AudioClip audioClip;
        
        [Tooltip("The blank animation clip where the baked keyframes will be saved. Put this in your FX layer!")]
        public AnimationClip animationClip;
        
        [Tooltip("Audio quality. 11 (2048 samples) is perfect for VRChat. Higher = more precise but slower to bake.")]
        [Range(7, 13)] public int fftResolution = 11; 

        [Header("Live Mode (Gameplay)")]
        [Tooltip("FOR UNITY TESTING ONLY. VRChat avatars do not support live C# audio scripts. Uncheck this before uploading your avatar!")]
        public bool enableLiveMode = false;
        
        [Tooltip("The AudioSource playing in your scene for live testing.")]
        public AudioSource liveAudioSource;

        [Serializable]
        public class RecordSlot
        {
            [Tooltip("Toggle this off to ignore this effect when baking. Great for testing!")]
            public bool isEnabled = true;

            [Tooltip("Name this effect (e.g., 'Bass Pulse' or 'Viseme Open').")]
            public string name = "New Effect";
            
            [Tooltip("The specific mesh, particle system, or light on your avatar you want to animate.")]
            public GameObject targetObject;
            
            [HideInInspector] public string bindingPath = "";
            [HideInInspector] public string bindingType = "";
            [HideInInspector] public string bindingProperty = "";

            [Tooltip("The lowest frequency to listen to. 20Hz = Deep sub-bass.")]
            public float startFreq = 20f;
            
            [Tooltip("The highest frequency to listen to. 250Hz = Kick drums and basslines.")]
            public float endFreq = 250f;
            
            [Tooltip("Boosts quiet songs. If your avatar isn't reacting enough, turn this up!")]
            public float volumeMultiplier = 5.0f;
            
            [Tooltip("Noise gate. 0.2 means it ignores background noise and only pulses on loud beats. Great for isolating kick drums.")]
            [Range(0f, 1f)] public float threshold = 0.2f;
            
            [Tooltip("Sculpt the punch! A steep curve makes the avatar mesh snap open instantly and ease back down.")]
            public AnimationCurve responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

            [Tooltip("Check this to animate Material Colors (like Emission for glowing tattoos) instead of a slider.")]
            public bool isColor = false;
            
            [Tooltip("The resting state value (e.g., 0 for a BlendShape).")]
            public float minValue = 0f;
            
            [Tooltip("The maximum flexed value (e.g., 100 for a BlendShape).")]
            public float maxValue = 1f;
            
            [Tooltip("The color it will glow when the beat hits max volume.")]
            public Gradient colorGradient = new Gradient();

            public UnityEvent<float> onLiveFloat;
            public UnityEvent<Color> onLiveColor;
        }

        [HideInInspector]
        public List<RecordSlot> recordSlots = new List<RecordSlot>();
        private float[] liveSpectrumData = new float[2048];

        void Update()
        {
            if (enableLiveMode && liveAudioSource != null && liveAudioSource.isPlaying)
            {
                liveAudioSource.GetSpectrumData(liveSpectrumData, 0, FFTWindow.BlackmanHarris);
                float freqPerBin = (AudioSettings.outputSampleRate / 2f) / liveSpectrumData.Length;

                foreach (var slot in recordSlots)
                {
                    if (!slot.isEnabled) continue; // Skip disabled slots

                    int startBin = Mathf.Clamp(Mathf.RoundToInt(slot.startFreq / freqPerBin), 0, liveSpectrumData.Length - 1);
                    int endBin = Mathf.Clamp(Mathf.RoundToInt(slot.endFreq / freqPerBin), 0, liveSpectrumData.Length - 1);

                    float peak = 0f;
                    for (int b = startBin; b <= endBin; b++) if (liveSpectrumData[b] > peak) peak = liveSpectrumData[b];
                    
                    peak = Mathf.Clamp01(peak * 10f * slot.volumeMultiplier);
                    if (peak < slot.threshold) peak = 0f;

                    float finalAmplitude = slot.responseCurve.Evaluate(peak);

                    if (slot.isColor) slot.onLiveColor?.Invoke(slot.colorGradient.Evaluate(finalAmplitude));
                    else slot.onLiveFloat?.Invoke(Mathf.Lerp(slot.minValue, slot.maxValue, finalAmplitude));
                }
            }
        }

        public float[] AnalyzeAudioChunk(float[] audioSamples, int logSize)
        {
            int halfSize = (1 << logSize) / 2;
            NativeArray<float> inputNative = new NativeArray<float>(audioSamples, Allocator.TempJob);
            NativeArray<float> outputNative = new NativeArray<float>(halfSize, Allocator.TempJob);

            BurstFFTJob fftJob = new BurstFFTJob { InputSamples = inputNative, OutputSpectrum = outputNative, LogSize = logSize };
            fftJob.Schedule().Complete();
            float[] spectrumResult = outputNative.ToArray();

            inputNative.Dispose();
            outputNative.Dispose();

            return spectrumResult;
        }

        public float[] GetLiveSpectrum(float timeInSeconds)
        {
            if (audioClip == null) return new float[0];
            int windowSize = 1 << fftResolution;
            int startSample = Mathf.Clamp(Mathf.RoundToInt(timeInSeconds * audioClip.frequency), 0, audioClip.samples - windowSize);
            
            float[] audioData = new float[windowSize * audioClip.channels];
            audioClip.GetData(audioData, startSample);

            float[] chunk = new float[windowSize];
            for (int j = 0; j < windowSize; j++) 
            {
                float mixed = 0;
                for(int c = 0; c < audioClip.channels; c++) mixed += audioData[j * audioClip.channels + c];
                chunk[j] = mixed / audioClip.channels;
            } 
            return AnalyzeAudioChunk(chunk, fftResolution);
        }

        public static string CalculateTransformPath(Transform targetTransform, Transform rootTransform)
        {
            if (targetTransform == null || rootTransform == null) return "";
            string returnName = targetTransform.name;
            Transform tempObj = targetTransform;
            if (tempObj == rootTransform) return "";
            while (tempObj.parent != null && tempObj.parent != rootTransform)
            {
                returnName = tempObj.parent.name + "/" + returnName;
                tempObj = tempObj.parent;
            }
            return returnName;
        }

        public void BakeVFXTexture()
        {
#if UNITY_EDITOR
            if (audioClip == null) return;

            int channels = audioClip.channels;
            int totalSamples = audioClip.samples;
            float[] audioData = new float[totalSamples * channels];
            audioClip.GetData(audioData, 0);

            int windowSize = 1 << fftResolution;
            int stepSize = windowSize / 2; 
            
            int width = totalSamples / stepSize;
            int height = 512; 

            Texture2D tex = new Texture2D(width, height, TextureFormat.RFloat, false);
            
            for (int x = 0; x < width; x++)
            {
                int sampleIndex = x * stepSize;
                if (sampleIndex + windowSize >= totalSamples) break;

                float[] chunk = new float[windowSize];
                for (int j = 0; j < windowSize; j++) chunk[j] = audioData[(sampleIndex + j) * channels]; 
                
                float[] spectrum = AnalyzeAudioChunk(chunk, fftResolution);
                
                for (int y = 0; y < height; y++)
                {
                    float normalizedY = (float)y / height;
                    int bin = Mathf.FloorToInt(Mathf.Pow(normalizedY, 2) * (spectrum.Length - 1));
                    tex.SetPixel(x, y, new Color(spectrum[bin] * 10f, 0, 0)); 
                }
            }
            tex.Apply();

            byte[] bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            string path = EditorUtility.SaveFilePanelInProject("Save VFX Audio Texture", audioClip.name + "_VFX", "exr", "Save Audio Texture");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();
                Debug.Log("VFX Audio Texture generated at: " + path);
            }
#endif
        }

        public IEnumerator ComputeAnimation(AnimationClip clip)
        {
            yield return null;
            if (audioClip == null || clip == null) yield break;

            int channels = audioClip.channels;
            int totalSamples = audioClip.samples;
            int frequency = audioClip.frequency;
            
            float[] audioData = new float[totalSamples * channels];
            audioClip.GetData(audioData, 0);

            int windowSize = 1 << fftResolution;
            int stepSize = windowSize / 4; 
            float maxFreq = frequency / 2f;
            
            Dictionary<RecordSlot, AnimationCurve[]> slotCurves = new Dictionary<RecordSlot, AnimationCurve[]>();
            foreach (var slot in recordSlots)
            {
                if (!slot.isEnabled || string.IsNullOrEmpty(slot.bindingProperty)) continue; // SKIP IF DISABLED!
                slotCurves[slot] = slot.isColor ? new AnimationCurve[4] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() } : new AnimationCurve[1] { new AnimationCurve() };
            }

            int totalSteps = totalSamples / stepSize;
            int currentStep = 0;

            for (int i = 0; i + windowSize < totalSamples; i += stepSize)
            {
                float[] chunk = new float[windowSize];
                for (int j = 0; j < windowSize; j++) 
                {
                    float mixed = 0;
                    for(int c = 0; c < channels; c++) mixed += audioData[(i + j) * channels + c];
                    chunk[j] = mixed / channels;
                } 

                float[] spectrum = AnalyzeAudioChunk(chunk, fftResolution);
                float timeInSeconds = (float)i / frequency; 
                float freqPerBin = maxFreq / spectrum.Length;

                foreach (var slot in recordSlots)
                {
                    if (!slotCurves.ContainsKey(slot)) continue;

                    int startBin = Mathf.Clamp(Mathf.RoundToInt(slot.startFreq / freqPerBin), 0, spectrum.Length - 1);
                    int endBin = Mathf.Clamp(Mathf.RoundToInt(slot.endFreq / freqPerBin), 0, spectrum.Length - 1);

                    float peak = 0f;
                    for (int b = startBin; b <= endBin; b++) if (spectrum[b] > peak) peak = spectrum[b];
                    
                    peak = Mathf.Clamp01(peak * 10f * slot.volumeMultiplier);
                    if (peak < slot.threshold) peak = 0f;

                    float finalAmplitude = slot.responseCurve.Evaluate(peak);

                    if (slot.isColor)
                    {
                        Color c = slot.colorGradient.Evaluate(finalAmplitude);
                        slotCurves[slot][0].AddKey(new Keyframe(timeInSeconds, c.r));
                        slotCurves[slot][1].AddKey(new Keyframe(timeInSeconds, c.g));
                        slotCurves[slot][2].AddKey(new Keyframe(timeInSeconds, c.b));
                        slotCurves[slot][3].AddKey(new Keyframe(timeInSeconds, c.a));
                    }
                    else
                    {
                        float val = Mathf.Lerp(slot.minValue, slot.maxValue, finalAmplitude);
                        slotCurves[slot][0].AddKey(new Keyframe(timeInSeconds, val));
                    }
                }

                currentStep++;
#if UNITY_EDITOR
                if (currentStep % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("Bit Animator", $"Baking Audio... {Mathf.RoundToInt((float)currentStep/totalSteps * 100)}%", (float)currentStep/totalSteps);
                    yield return null; 
                }
#endif
            }

            foreach (var slot in recordSlots)
            {
                if (!slotCurves.ContainsKey(slot)) continue;
                Type componentType = Type.GetType(slot.bindingType) ?? typeof(UnityEngine.Object).Assembly.GetType(slot.bindingType);
                if (componentType == null) continue;

                for (int p = 0; p < slotCurves[slot].Length; p++)
                {
                    for (int k = 0; k < slotCurves[slot][p].keys.Length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(slotCurves[slot][p], k, AnimationUtility.TangentMode.Auto);
                        AnimationUtility.SetKeyRightTangentMode(slotCurves[slot][p], k, AnimationUtility.TangentMode.Auto);
                    }
                }

                if (slot.isColor)
                {
                    string baseProp = slot.bindingProperty;
                    if (baseProp.EndsWith(".r") || baseProp.EndsWith(".g") || baseProp.EndsWith(".b") || baseProp.EndsWith(".a"))
                        baseProp = baseProp.Substring(0, baseProp.Length - 2);

                    clip.SetCurve(slot.bindingPath, componentType, baseProp + ".r", slotCurves[slot][0]);
                    clip.SetCurve(slot.bindingPath, componentType, baseProp + ".g", slotCurves[slot][1]);
                    clip.SetCurve(slot.bindingPath, componentType, baseProp + ".b", slotCurves[slot][2]);
                    clip.SetCurve(slot.bindingPath, componentType, baseProp + ".a", slotCurves[slot][3]);
                }
                else
                {
                    clip.SetCurve(slot.bindingPath, componentType, slot.bindingProperty, slotCurves[slot][0]);
                }
            }

#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(clip);
#endif
            Debug.Log($"[BitAnimator] Finished baking to {clip.name}!");
        }

        public void StartBaking()
        {
            if (animationClip != null) { animationClip.ClearCurves(); animationClip.frameRate = 60f; }
        }
    }
}