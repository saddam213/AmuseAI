
<p align="center">
   <img width="30%" src="Assets/Amuse-Logo-512.png">
</p>
<div align="center">
   <h2><a href="https://github.com/TensorStack-AI/AmuseAI/releases/download/v3.2.0/Amuse_v3.2.0.exe">Download Amuse v3.2.0</a></h2>
</div>



## Amuse Roadmap
* ~~**.NET 10 + OnnxRuntime Latest:** Migration to the .NET 10 framework and integration of the latest OnnxRuntime for optimized hardware acceleration.~~ (v3.2.0)
* **Python Inference Runtime:** Implementation of a dedicated Python backend to provide seamless interop with the broader machine learning ecosystem.
* **LoRA Adapters:** Support for dynamic loading and blending of LoRA adapters for fine-tuned style and character control.
* **Z-Image & FLUX2 Models:** Integration of next-generation transformer architectures, including Z-Image and FLUX2, for high-fidelity image synthesis.
* **WAN & LTX-2 Video Models:** Expansion into generative video using WAN and LTX-2 architectures for advanced motion and temporal consistency.
* **STT + TTS Audio Models:** Deployment of Speech-to-Text for voice-driven prompting and Text-to-Speech for high-quality audio generation.

---

## Required External Plugins
1. `ContentFilter` add `ContentFilter.onnx` & `ContentFilter.bin` to `Plugins\ContentFilter`
2. `CLIPTokenizer` add tokenizer files to `Plugins\CLIPTokenizer`

Note: Easy way is to just install the latest Amuse version and copy the files from the `X:\Program Files\Amuse\Plugins` directory

---

## Required External Licences

`ImageSharp v3` Licence Required https://sixlabors.com/pricing/
1. Add licence file to root project directory

`FontAwesome Pro v6` Licence Required https://fontawesome.com/v6/download
1. Download the `6.7.2 for the desktop` package
2. Add the font files to the `Fonts` directory
3. Set build action to `Resource` for all font files

---

## Archive:
https://huggingface.co/TensorStack/Amuse
