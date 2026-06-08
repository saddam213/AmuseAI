
<p align="center">
   <img width="30%" src="Assets/Amuse-Logo-512.png">
</p>
<div align="center">
   <h1><a href="https://github.com/TensorStack-AI/AmuseAI/releases/download/v3.5.0/Amuse_v3.5.1.exe">Download Amuse v3.5.1</a></h1>
</div>

[![GitHub Release](https://img.shields.io/github/v/release/TensorStack-AI/AmuseAI?include_prereleases&label=Amuse&color=%2344cc11)](https://github.com/TensorStack-AI/AmuseAI/releases)
[![Common Badge](https://img.shields.io/nuget/v/TensorStack.Common?color=4bc51e&label=TensorStack)](https://www.nuget.org/packages/TensorStack.Common)
![Nuget](https://img.shields.io/nuget/dt/TensorStack.Common?label=Nuget%20Downloads)
[![GitHub last commit](https://img.shields.io/github/last-commit/TensorStack-AI/AmuseAI)](https://github.com/TensorStack-AI/AmuseAI/commits/master/)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/TensorStack-AI/AmuseAI/total)
[![Discord](https://img.shields.io/discord/1457477275246268451?label=Discord&)]([https://discord.gg/ptgMMv36Xu](https://discord.gg/ptgMMv36Xu))



# Amuse
Amuse is the flagship demo application for the [TensorStack SDK](https://github.com/TensorStack-AI/TensorStack), showcasing high-performance local AI image, video, audio and text generation through a modern, extensible .NET architecture.

<p align="center" width="100%">
    <img src="Assets/Screenshots/TextToImage.PNG">
</p>



## Features
* Automatic installation of an isolated, Python environment.
* Safetensors, GGUF, and ONNX support.
* Video Editor for generated or local content.
* Image/Video Upscale for static and moving media.
* Feature Extraction from images and video.
* Video Interpolation for frame rates and slow-motion.
* Image Inpaint to remove objects or fill areas.
* Advanced Image Editing with selection and masking tools.
* Voice Generation (Supertonic).
* Speech Recognition (Whisper).
* Media Gallery for organization and management.
* Lora/ControlNet Support for output control.

---

## Image Pipelines
- Z-Image
- Qwen
- FLUX.1
- FLUX.2
- Chroma
- Kandinsky5
- StableDiffusion-XL
- StableDiffusion-3

## Video Pipelines
- LTX
- LTX-2
- Wan 2.2
- CogVideoX
- Kandinsky5
- SkyReels-V2
- Helios

## Audio Pipelines
- ACE-Step XL
- Whisper
- Supertonic

---

## GPU Support
Amuse utilizes `NVIDIA CUDA 12.8` and `AMD ROCm 7.2` for local hardware acceleration.

### Nvidia GPU Support
Amuse leverages `CUDA 12.8`, providing native support for the latest generation of hardware.<br /> While legacy architectures (Pascal/Maxwell) are technically supported, an RTX-enabled card is strongly recommended to utilize Tensor Cores for efficient generation speeds.
<table>
  <thead>
    <tr>
      <th>Architecture</th>
      <th>Platform Support</th>
      <th>GPU Models</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><b>Blackwell</b> (SM_100)</td>
      <td>Windows 10, 11, Server 22</td>
      <td>GeForce RTX 5090, 5080, 5070 Ti, 5070; RTX PRO Blackwell series</td>
    </tr>
    <tr>
      <td><b>Ada Lovelace</b> (SM_89)</td>
      <td>Windows 10, 11, Server 22</td>
      <td>GeForce RTX 4090, 4080, 4070 Ti/Super, 4070, 4060 Ti, 4060; RTX 6000/5000/4000 Ada</td>
    </tr>
    <tr>
      <td><b>Ampere</b> (SM_86)</td>
      <td>Windows 10, 11, Server 22</td>
      <td>GeForce RTX 3090 Ti, 3090, 3080 Ti, 3080, 3070 Ti, 3070, 3060 Ti, 3060; RTX A-series (A6000, etc.)</td>
    </tr>
    <tr>
      <td><b>Turing</b> (SM_75)</td>
      <td>Windows 10, 11, Server 22</td>
      <td>GeForce RTX 2080 Ti, 2080 Super, 2070, 2060; GTX 1660 Ti, 1660 Super, 1650</td>
    </tr>
  </tbody>
</table>

> Note: Minimum Driver (NVIDIA): `Version 527.41` or later is required for `CUDA 12.8` compatibility.

---

### AMD GPU Support
Amuse currently only supports the `7000 series` GPU's (7900XTX & W7900)<br/>
For `AMD` devices I recommend `ComfyUI` or `AMD Lemonade Server`, these will have way better support for AMD.

ComfyUI for AMD:
https://www.amd.com/en/blogs/2026/amd-comfyui-advancing-professional-quality-generative-ai-ryzen-radeon.html

Lemonade Server for AMD
https://lemonade-server.ai/

---

## Demo Videos

### Video Editor
https://github.com/user-attachments/assets/bc7968e6-0e3a-42c9-b08f-ec47476d1d22

---

### VideoFrame Viewer
https://github.com/user-attachments/assets/95989ba0-24f8-4a8b-ba6b-f68ec173d304

---

### Image Edit
https://github.com/user-attachments/assets/cec3838f-57d6-48f2-8ceb-5dd0634a7fbc

---

### Automations
https://github.com/user-attachments/assets/ce761642-caa8-4c35-b814-77079ee2f271
