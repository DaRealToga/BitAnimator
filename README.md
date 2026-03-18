# Bit Animator Pro

A modernized, high-performance audio visualizer and animation baker for `Unity 2022.3.22f1`. Designed for VRChat Avatar creators, VTubers, and Unity VFX artists.

Bit Animator Pro analyzes audio tracks using an optimized Burst-compiled FFT engine, baking precise, beat-synced keyframes directly into Unity AnimationClips. Drive blendshapes, particle emissions, material colors, and transform scales based on frequency data.

*Original repository: [Leviant/BitAnimator](https://github.com/Leviant/BitAnimator)* — Fully rewritten, modernized, and optimized by darealtoga.

---

## Features
* **Zero-Dependency UI:** A native Unity Inspector with collapsible modules, interactive foldouts, and automated property dropdowns. 
* **Burst-Compiled FFT Engine:** Executes audio calculations in milliseconds without blocking the main editor thread.
* **Frequency Sculpting:** Isolate specific frequency bands (e.g., 20Hz-250Hz for Kick Drums). Utilize threshold gates and response curves to generate precise animations rather than standard audio averages.
* **VFX Graph Integration:** Bake audio tracks into an `.EXR` texture file to drive GPU particles in Unity VFX Graph.
* **Preset System:** Serialize and save module configurations to `.json` files for cross-project sharing.
* **Live Testing Mode:** Evaluate visualizer configurations in real-time within the Unity Editor prior to baking. 

---

## Installation

### Prerequisites
This tool relies on Unity's high-performance multi-threading. Ensure the following native Unity packages are installed via the Package Manager (`Window > Package Manager > Unity Registry`):
1. `Burst` (com.unity.burst)
2. `Mathematics` (com.unity.mathematics)
3. `Collections` (com.unity.collections)

### Setup
1. Clone this repository or download the latest release.
2. Place the `BitAnimator` directory directly into your Unity project's `Assets` folder.
3. Ensure your target audio track (`.mp3`, `.wav`, `.ogg`) has its **Load Type** set to `Decompress On Load` in the Unity Import Settings to allow the math engine to read the sample data.

---

## Usage Guide (VRChat Workflow)

**1. Scene Preparation**
* Create an Empty GameObject and attach the `BitAnimator` component.
* Assign your Avatar's root `Animator` component.
* Assign your target `Audio Clip`.
* Create a blank `AnimationClip` in your project and assign it to the `Target Animation` field.

**2. Module Configuration**
* Click **+ Add Effect Module**.
* Assign the mesh, particle system, or material to the `Target Object` field.
* Use the **Animate Property** dropdown to select the specific parameter to drive (e.g., `SkinnedMeshRenderer / blendShape.vrc.v_aa`).

**3. Audio Calibration**
* **Start/End Freq:** Define the isolation band. (Bass/Kicks: `20-250Hz`. Vocals: `250-4000Hz`. Cymbals: `4000Hz+`).
* **Noise Gate (Threshold):** Adjust to filter background frequencies. A value of `0.3` ensures activation only on peak volumes.
* **Min/Max Value:** Define the lower and upper bounds of the resulting animation curve.

**4. Baking the Animation**
* Click **BAKE ALL ANIMATIONS**. 
* The system will analyze the audio and write the keyframes to your assigned Animation Clip.
* Add this Animation Clip to your VRChat FX Layer.

---

## Color Mapping & VFX Graph

* **Color Mapping:** Enable `Drive Colors Instead` on any module to replace standard floats with a Gradient editor. The system will automatically map the RGBA channels into your animation clip to drive material colors.
* **VFX Graph Exporter:** Use `Bake Audio to VFX Graph Texture` to generate an audio heat-map. Import this `.EXR` into a VFX Graph `Sample Texture` node, mapping particle Lifetime to the X-axis (Time) and particle ID to the Y-axis (Frequency).

---

## Credits & License
* **DaRealToga** - Core engine rewrite, UI architecture, and Burst compiler integration.
* **Leviant** - Original concept and legacy architecture. 

*See the LICENSE file for usage rights.*
