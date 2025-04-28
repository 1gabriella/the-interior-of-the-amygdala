# The Interior of the Amygdala

A Virtual Reality experience built in Unity, showcasing interactive environments, AI-driven chatbots, emotion detection, audio-reactive visuals, and a mini-map system.

---

## 📑 Table of Contents

- [🌟 Features](#-features)  
- [🎯 Prerequisites](#-prerequisites)  
- [🛠️ Project Setup](#-project-setup)  
- [🔑 Hugging Face Integration](#-hugging-face-integration)  
- [🚀 Usage](#-usage)  
  - [In-Editor Testing](#in-editor-testing)  
  - [Building for a VR Headset (Oculus Quest 3S)](#building-for-a-vr-headset-oculus-quest-3s)  
- [📂 Folder Structure](#-folder-structure)  

---

## 🌟 Features

- Built with **Unity XR Interaction Toolkit**  
- AI-driven chatbot UI (`FlipPhoneChatXR`)  
- Real-time speech transcription (`SpeechRecognitionTest`)  
- Emotion detection via Hugging Face (`EmotionDetectionAPI`)  
- Audio-reactive FFT visuals & guitar simulation (`SimpleMusicVA`)  
- Interactive draggable environment & 2D mini-map (`MinimapSystem`)  


---

## 🎯 Prerequisites

- **Unity**: 2021.3 LTS or newer  
- **.NET** Scripting Runtime: .NET 4.x Equivalent  
- **Git** v2.28+ with **Git LFS** installed  
- **Android SDK & NDK** (for Oculus Quest builds)  
- **Unity Packages** (via Package Manager):  
  - XR Interaction Toolkit  
  - Oculus XR Plugin  
  - Input System  
  - TextMeshPro  
  - Unity UI  

---

## 🛠️ Project Setup

1. **Clone the repository**  
   ```bash
   git clone https://github.com/1gabriella/VR-Nostalgia-Final.git
   cd VR-Nostalgia-Final
git lfs install
git lfs pull

## Open in Unity

1. **Add project to Unity Hub**  
   - In Unity Hub: **Add** → select the project folder  

2. **Configure Asset Serialization**  
   - In **Project Settings → Editor**:  
     - Enable **Visible Meta Files**  
     - Set **Asset Serialization** to **Force Text**  



## 🔑 Hugging Face Integration

1. **Get an API Token**  
   - Sign in at [huggingface.co](https://huggingface.co) → **Settings → Access Tokens → New Token** (grant read access)  

2. **Assign your token**  
   - In the Inspector, paste your token into the `apiKey` fields for:
     - **FlipPhoneChatXR**  
     - **SpeechRecognitionTest**  
<img width="1024" alt="Screenshot 2025-04-21 at 01 55 00" src="https://github.com/user-attachments/assets/fce952df-385e-4995-a0a9-ef79beb1385a" />

3. **Model Endpoints**  
   - **Chat**: `microsoft/DialoGPT-medium`  
   - **ASR**: `facebook/wav2vec2-base-960h`  
   - **Emotion**: `1bbypluto/Nostalgic_finetuned`  

---

## 🚀 Usage

### In-Editor Testing

- Press **Play** in the Unity Editor.  
- **Note:** player to movearound requires a connected VR headset.

### Building for a VR Headset (Oculus Quest 3S)

1. **Switch build target**  
   - File → **Build Settings** → select **Android** → **Switch Platform**  
   - Add your current scene under **Scenes In Build**

2. **Player Settings**  
   - **Minimum API Level:** Android 7.0 or higher  
   - **XR Plug-in Management:** ensure **Oculus** is checked  
<img width="884" alt="Screenshot 2025-04-20 at 14 51 29" src="https://github.com/user-attachments/assets/105fd494-2cd7-4bf2-b85d-2425d4a4a67b" />

3. **Build & Run**  
   - Connect your Quest via USB 
   - Click **Build and Run** → Unity will deploy the APK to your headset
   - First build may take a while 

4. **Test on Quest**  
   - Launch the app under **Unknown Sources** in  meta quest
   - note you will need the meta quest app and have developer settings enabled

