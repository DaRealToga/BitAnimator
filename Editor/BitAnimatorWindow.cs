// Copyright © 2021 Leviant. (The Interactive Spectrogram Visualizer)
using UnityEngine;
using UnityEditor;
using AudioVisualization;

namespace AudioVisualization.Editor
{
    public class BitAnimatorWindow : EditorWindow
    {
        private BitAnimator targetAnimator;
        private int targetSlotIndex;
        private float previewTime = 0f;
        
        // Visualizer config
        private const float MAX_DISPLAY_FREQ = 8000f; // Limit to 8kHz so bass/mids take up most of the screen
        private float[] currentSpectrum;

        public static void ShowWindow(BitAnimator animator, int slotIndex)
        {
            BitAnimatorWindow window = GetWindow<BitAnimatorWindow>("Audio Spectrogram");
            window.targetAnimator = animator;
            window.targetSlotIndex = slotIndex;
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnGUI()
        {
            if (targetAnimator == null || targetAnimator.audioClip == null || targetAnimator.recordSlots.Count <= targetSlotIndex)
            {
                EditorGUILayout.HelpBox("Please assign an AudioClip to the BitAnimator and ensure the slot exists.", MessageType.Warning);
                return;
            }

            BitAnimator.RecordSlot slot = targetAnimator.recordSlots[targetSlotIndex];
            AudioClip clip = targetAnimator.audioClip;

            GUILayout.Label($"Editing Frequencies for: {slot.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Timeline Scrubber
            EditorGUILayout.LabelField("Timeline Scrubber (Seconds)");
            EditorGUI.BeginChangeCheck();
            previewTime = EditorGUILayout.Slider(previewTime, 0f, clip.length);
            if (EditorGUI.EndChangeCheck() || currentSpectrum == null)
            {
                // Fetch live Burst FFT data for this exact millisecond
                currentSpectrum = targetAnimator.GetLiveSpectrum(previewTime);
                Repaint();
            }

            EditorGUILayout.Space();

            // --- DRAW THE SPECTROGRAM ---
            Rect visualizerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(150), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(visualizerRect, new Color(0.1f, 0.1f, 0.1f)); // Dark background

            if (currentSpectrum != null && currentSpectrum.Length > 0)
            {
                float freqPerBin = (clip.frequency / 2f) / currentSpectrum.Length;
                int maxBinToDraw = Mathf.RoundToInt(MAX_DISPLAY_FREQ / freqPerBin);
                maxBinToDraw = Mathf.Min(maxBinToDraw, currentSpectrum.Length);

                float barWidth = visualizerRect.width / maxBinToDraw;

                // Draw the frequency bars
                for (int i = 0; i < maxBinToDraw; i++)
                {
                    float height = Mathf.Clamp01(currentSpectrum[i] * slot.volumeMultiplier) * visualizerRect.height;
                    Rect barRect = new Rect(visualizerRect.x + (i * barWidth), visualizerRect.yMax - height, barWidth, height);
                    
                    // Highlight the bars that fall inside our current selection
                    float currentFreq = i * freqPerBin;
                    Color barColor = (currentFreq >= slot.startFreq && currentFreq <= slot.endFreq) ? Color.cyan : Color.gray;
                    
                    EditorGUI.DrawRect(barRect, barColor);
                }

                // Draw the Threshold Line
                float thresholdY = visualizerRect.yMax - (slot.threshold * visualizerRect.height);
                EditorGUI.DrawRect(new Rect(visualizerRect.x, thresholdY, visualizerRect.width, 1), Color.red);
            }

            EditorGUILayout.Space();
            
            // --- THE INTERACTIVE RANGE SLIDER ---
            EditorGUILayout.LabelField("Drag to Isolate Frequencies (Cyan Bars)", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            float tempStart = slot.startFreq;
            float tempEnd = slot.endFreq;
            
            EditorGUILayout.MinMaxSlider(ref tempStart, ref tempEnd, 0f, MAX_DISPLAY_FREQ);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetAnimator, "Change Frequency Range");
                slot.startFreq = Mathf.Round(tempStart);
                slot.endFreq = Mathf.Round(tempEnd);
                EditorUtility.SetDirty(targetAnimator);
                Repaint();
            }

            EditorGUILayout.BeginHorizontal();
            slot.startFreq = EditorGUILayout.FloatField("Start (Hz)", slot.startFreq);
            slot.endFreq = EditorGUILayout.FloatField("End (Hz)", slot.endFreq);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Drag the timeline to find a kick drum or vocal note. Drag the handles above to surround the frequency spikes (Cyan). The red line shows your current Threshold cutoff.", MessageType.Info);
        }
    }
}