# Installing Amuse

[![Getting started with Amuse](../images/getting-started/video-01-thumbnail.jpg)](https://youtu.be/coa8E5XWKUg)

*Video: Getting started with Amuse. Install, download your first model and create your first image (4 min 15 s).*

---

## System requirements

| | Minimum | Recommended |
|---|---|---|
| **Operating system** | Windows 10 (version 1809 or later), 64-bit | Windows 11, 64-bit |
| **Graphics card** | Any GPU with Vulkan support | NVIDIA RTX, or a recent AMD Radeon card |
| **Video memory (VRAM)** | 8 GB for smaller image models | 16 GB or more for video and larger models |
| **Disk space** | 20 GB free for a few models | 100 GB or more on a fast SSD |

Amuse runs models on your graphics card through **backends**:

| Backend | Used for | NVIDIA | AMD | Intel |
|---|---|---|---|---|
| StableDiffusion.cpp | Images and video | CUDA or Vulkan | ROCm or Vulkan | Vulkan |
| Audio.cpp | Music, speech and transcription | CUDA or Vulkan | Vulkan | Vulkan |
| Llama.cpp | Language models (chat, instruct, image to text) | CUDA or Vulkan | ROCm or Vulkan | Vulkan |
| ONNX Runtime | Upscalers, extractors and interpolation | DirectML | DirectML | DirectML |

You don't need to install any of these yourself. Amuse downloads the right one for your hardware the first time it starts (see [First run](first-run.md)).

> **NVIDIA:** CUDA backends need an up-to-date driver. CUDA 13 requires driver version `580.65` or later. If you can't update your driver, choose the CUDA 12 or Vulkan option instead.

> **How much memory does a model need?** Every model on the **Models** page shows its memory size (for example *FLUX.2 Klein 4B: 10 GB* at Q8_0). Lower quality versions such as Q4_0 need less. If a model is close to your VRAM size, the **Memory** setting next to the model can lower how much video memory it uses, at some cost in speed.

---

## Download and install

1. Go to the **[Amuse releases page](https://github.com/TensorStack-AI/AmuseAI/releases/latest)**.
2. Under **Assets**, download the Amuse installer, **Amuse_v4.0.0.exe**.
3. Run the installer and follow the prompts.
4. Start **Amuse** from the Start menu.

The first time Amuse starts, the setup window opens. Continue with **[First run](first-run.md)**.

### Where Amuse keeps its files

| What | Default location | Can be changed |
|---|---|---|
| Settings and logs | `%LOCALAPPDATA%\Amuse` | No |
| Models | Chosen in the setup window | Yes: **Settings → General → Model Folder** |
| History (everything you create) | `%LOCALAPPDATA%\Amuse\History` | Yes: **Settings → General** |
| Temporary files | `%LOCALAPPDATA%\Amuse\Temp` | Yes: **Settings → General** |

Models are the biggest files by far: a single image model is usually 4 to 20 GB, and video models can be larger. Choose a model folder on a drive with plenty of space.

---

## Updating Amuse

When **Check For Updates** is turned on, Amuse checks GitHub when it starts and shows **New Version!** at the top of the window when one is available. Click it for the details, then download and run the new installer from the releases page. Your settings, models and history are kept.

---

## Troubleshooting

| Problem | What to try |
|---|---|
| Backend install fails on Windows 11 | Close Amuse, right-click it and choose **Run as administrator**, then install the backend again from **Home → Manage Environments**. |
| CUDA backend fails to load | Update your NVIDIA driver, or install the Vulkan backend instead. |
| A model runs very slowly | It's probably larger than your VRAM. Download a lower quality version (for example Q4_0) or a smaller model. |
| Out of disk space | Move the model folder to another drive from **Settings → General**, or delete models you don't use from **Settings**. |

---

**Next:** [First run →](first-run.md)
