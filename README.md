# Amuse
Local AI image, video, audio and text.

## Features
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
- LLaDA Image

## Video Pipelines
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


## How To Run
Amuse is built on **.NET 10** and is developed using **Visual Studio**.

To build and Run Amuse:

1. Install **Visual Studio 2026** with the **.NET desktop development** workload.
2. Clone the repository and open the `.sln` solution file in Visual Studio.
3. Allow Visual Studio to restore the required NuGet packages and dependencies.
4. Select the desired build configuration (`Debug` or `Release`).
5. Build the solution using **Build → Build Solution** or `Ctrl+Shift+B`.

The resulting binaries will be available in the project's configured build output directory.

Run `Amuse.exe` to start Amuse.


---


## Contributors
Contributions to Amuse are welcome!<br />
Whether you want to contribute code, report issues, suggest improvements, or help in other ways, your contributions are appreciated.

Thank you to everyone who contributes to Amuse and helps make the project better!

---

## License

Amuse is licensed under the Apache License 2.0.

> **Note:** Compiled binaries are licensed under the Amuse 2.0 License due to the inclusion of bundled third-party software and dependencies, which may be subject to their own licensing requirements.

---

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