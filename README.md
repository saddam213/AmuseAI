# Amuse
Amuse is the flagship demo application for the [TensorStack SDK](https://github.com/saddam213/TensorStack), showcasing high-performance local AI image, video, audio and text generation through a modern, extensible .NET architecture.

<div align="center">
   <h1><a href="https://github.com/saddam213/AmuseAI/releases/download/v3.7.9/Amuse_v3.7.9.exe">Download Amuse v3.7.9</a></h1>
</div>

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
- Ernie Image
- Anima
- JoyAI Image
- PRX-Pixel
- Krea2
- GLM Image
- Ideogram 4
- Boogu
- Lens
- LongCat
- HiDream-O1

## Video Pipelines
- LTX
- LTX-2
- Wan 2.2
- CogVideoX
- Kandinsky5
- SkyReels-V2
- Helios
- Motif
- AnyFlow
- MiniMax-H3

## Audio Pipelines
- ACE-Step XL
- Whisper
- Supertonic v3
- LongCat Audio

## Text Pipelines
- Qwen 3.0
- Qwen 3.5
- Qwen 3.6
- Qwen 3.8
- Gemma 4.0
---

## GPU Support
Amuse provides multiple GPU backends, allowing you to choose the best option for your hardware and operating system.

### CUDA (Nvidia)
NVIDIA GPUs can use **CUDA 13.0** for native GPU acceleration. RTX-enabled cards are strongly recommended for the best generation performance, as they provide Tensor Cores specifically designed to accelerate AI workloads. Legacy architectures such as Pascal and Maxwell may work, but are not recommended for optimal performance.

> NVIDIA driver version `580.65` or later is required for CUDA 13.0 compatibility.

### Vulkan (AMD, Nvidia, Intel)
**Vulkan** provides a cross-vendor GPU backend that can be used on compatible hardware without requiring CUDA or ROCm. This makes it particularly useful for GPUs and systems that do not have access to a supported CUDA or ROCm environment.

>  Vulkan support depends on the capabilities of the installed GPU and Vulkan driver.

---

> **Windows 11:** Users may need to `Run As Administrator` during environment creation if environment setup fails.

> **Note:** Actual GPU compatibility and performance depend on the GPU model, driver version, operating system, and backend being used.

---



<div align="center">
   <h1><a href="https://github.com/saddam213/AmuseAI/releases/download/v3.7.9/Amuse_v3.7.9.exe">Download Amuse v3.7.9</a></h1>
</div>


## Attribution & Dependencies

Amuse is made possible in part by these excellent open-source projects
- `PdfPig` https://github.com/UglyToad/PdfPig
- `Markdig` https://github.com/xoofx/markdig
- `Serilog` https://github.com/serilog/serilog
- `ColorCode` https://github.com/CommunityToolkit/ColorCode-Universal
- `TensorStack` https://github.com/saddam213/TensorStack
- `Diffusers` https://github.com/huggingface/diffusers
- `Transformers` https://github.com/huggingface/transformers
- `StableDiffusion.cpp` https://github.com/leejet/stable-diffusion.cpp

---

## Contributors
Want to contribute to Amuse? I'd love to hear from you!

All development, source-code changes, issues, pull requests, and other contributions should be made through the main development repository:

https://github.com/saddam213/TensorStack

This repository is dedicated **solely to compiled release builds** of Amuse. Please use the main development repository to report issues, suggest improvements, contribute code, or get involved with the project.

Thank you to everyone who contributes to Amuse and helps make the project better!

---

## License

The compiled builds distributed in this repository are covered under the **Amuse 2.0 License**. This licensing applies to the compiled releases due to the inclusion of bundled third-party software and their respective licenses.

If you compile Amuse yourself from the main development repository:

https://github.com/saddam213/TensorStack

the Amuse source code is licensed under the **Apache License 2.0**. When creating your own compiled builds, you are responsible for obtaining, including, and complying with the licenses of any third-party software and dependencies you choose to bundle.

- **Compiled releases:** Amuse 2.0 License
- **Amuse source code:** Apache License 2.0
- **Self-compiled builds:** You are responsible for the licensing of bundled third-party software

---
