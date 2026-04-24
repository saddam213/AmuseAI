using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TensorStack.Image;
using TensorStack.Video;

namespace Amuse.App.Services
{
    public static class AutomationManager
    {
        /// <summary>
        /// Create diffusion automation jobs
        /// </summary>
        /// <param name="automationOptions">The automation options.</param>
        /// <param name="diffusionOptions">The diffusion options.</param>
        /// <param name="outputMediaType">Type of the output media.</param>
        /// <param name="inputMediaType">Type of the input media.</param>
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, DiffusionInputOptions diffusionOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.Seed)
            {
                var seeds = GetSeeds(0, automationOptions.Count, diffusionOptions.Seed);
                foreach (var (index, seed) in seeds.Index())
                {
                    var options = diffusionOptions with { Seed = seed };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        DiffusionOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Diffusion_{seed}")
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.PromptLines)
            {
                var promptLines = await File.ReadAllLinesAsync(automationOptions.InputFile);
                var seeds = GetSeeds(diffusionOptions.Seed, promptLines.Length);
                foreach (var (index, prompt) in promptLines.Index())
                {
                    if (string.IsNullOrWhiteSpace(prompt))
                        continue;

                    var seed = seeds[index];
                    var name = $"Line{index + 1}";
                    var options = diffusionOptions with
                    {
                        Seed = seed,
                        Prompt = $"{prompt} {diffusionOptions.Prompt}"
                    };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        DiffusionOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Diffusion_{name}_{seed}")
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.PromptFiles)
            {
                var promptFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.txt").ToArray();
                var seeds = GetSeeds(diffusionOptions.Seed, promptFiles.Length);
                foreach (var (index, promptFile) in promptFiles.Index())
                {
                    var seed = seeds[index];
                    var name = Path.GetFileNameWithoutExtension(promptFile);
                    var prompt = await File.ReadAllTextAsync(promptFile);
                    if (string.IsNullOrWhiteSpace(prompt))
                        continue;

                    var options = diffusionOptions with
                    {
                        Seed = seed,
                        Prompt = $"{prompt} {diffusionOptions.Prompt}"
                    };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        DiffusionOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Diffusion_{name}_{seed}")
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Image)
                {
                    var imageFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.png").ToArray();
                    var seeds = GetSeeds(diffusionOptions.Seed, imageFiles.Length);
                    foreach (var (index, filename) in imageFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var image = await ImageInput.CreateAsync(filename);
                        var options = diffusionOptions with { Seed = seed };
                        if (automationOptions.UseInputSize)
                        {
                            options.Width = image.Width;
                            options.Height = image.Height;
                        }

                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            DiffusionOptions = options,
                            InputImages = [image],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Diffusion_{name}_{seed}")
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.mp4").ToArray();
                    var seeds = GetSeeds(diffusionOptions.Seed, videoFiles.Length);
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            DiffusionOptions = diffusionOptions with { Seed = seed },
                            VideoStreams = [videoStream],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Diffusion_{name}_{seed}")
                        };
                    }
                }
            }
        }


        /// <summary>
        /// Create upscale automation jobs
        /// </summary>
        /// <param name="automationOptions">The automation options.</param>
        /// <param name="upscaleOptions">The upscale options.</param>
        /// <param name="outputMediaType">Type of the output media.</param>
        /// <param name="inputMediaType">Type of the input media.</param>
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, UpscaleInputOptions upscaleOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Image)
                {
                    var imageFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.png").ToArray();
                    foreach (var (index, filename) in imageFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var image = await ImageInput.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = imageFiles.Length,
                            InputImages = [image],
                            UpscaleOptions = upscaleOptions with { },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Upscale_{name}")
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.mp4").ToArray();
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = videoFiles.Length,
                            VideoStreams = [videoStream],
                            UpscaleOptions = upscaleOptions with { },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Upscale_{name}")
                        };
                    }
                }
            }
        }


        /// <summary>
        /// Create extract automation jobs
        /// </summary>
        /// <param name="automationOptions">The automation options.</param>
        /// <param name="extractOptions">The extract options.</param>
        /// <param name="outputMediaType">Type of the output media.</param>
        /// <param name="inputMediaType">Type of the input media.</param>
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, ExtractInputOptions extractOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Image)
                {
                    var imageFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.png").ToArray();
                    foreach (var (index, filename) in imageFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var image = await ImageInput.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = imageFiles.Length,
                            InputImages = [image],
                            ExtractOptions = extractOptions with { },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Extract_{name}")
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.mp4").ToArray();
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = videoFiles.Length,
                            VideoStreams = [videoStream],
                            ExtractOptions = extractOptions with { },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Extract_{name}")
                        };
                    }
                }
            }
        }


        /// <summary>
        /// Create interpolate automation jobs
        /// </summary>
        /// <param name="automationOptions">The automation options.</param>
        /// <param name="interpolateOptions">The interpolate options.</param>
        /// <param name="outputMediaType">Type of the output media.</param>
        /// <param name="inputMediaType">Type of the input media.</param>
        /// <returns>A Task&lt;IReadOnlyList`1&gt; representing the asynchronous operation.</returns>
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, InterpolateInputOptions interpolateOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = Directory.EnumerateFiles(automationOptions.InputDirectory, "*.mp4").ToArray();
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = videoFiles.Length,
                            VideoStreams = [videoStream],
                            InterpolateOptions = interpolateOptions with{ },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"Interpolate_{name}")
                        };
                    }
                }
            }
        }


        /// <summary>
        /// Gets the output filename.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="fileId">The file identifier.</param>
        private static string GetOutputFile(AutomationOptions options, MediaType mediaType, string fileId)
        {
            if (!options.UseOutputDirectory)
                return null;

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                return null;

            var ext = GetExtesnion(mediaType);
            var filename = $"{fileId}.{ext}";
            return Path.Combine(options.OutputDirectory, filename);
        }


        /// <summary>
        /// Gets the seeds.
        /// </summary>
        /// <param name="initialSeed">The initial seed.</param>
        /// <param name="count">The count.</param>
        /// <param name="baseSeed">The base seed.</param>
        private static int[] GetSeeds(int initialSeed, int count, int baseSeed = 0)
        {
            if (initialSeed == 0)
            {
                baseSeed = baseSeed == 0 ? Random.Shared.Next() : baseSeed;
                var random = new Random(baseSeed);
                return [baseSeed, .. Enumerable.Range(0, count - 1).Select(i => random.Next())];
            }
            return Enumerable.Repeat(initialSeed, count).ToArray();
        }


        /// <summary>
        /// Gets the file extesnion.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private static string GetExtesnion(MediaType mediaType)
        {
            return mediaType switch
            {
                MediaType.Text => "txt",
                MediaType.Audio => "wav",
                MediaType.Video => "mp4",
                MediaType.Image => "png",
                _ => throw new NotSupportedException()
            };
        }

    }
}