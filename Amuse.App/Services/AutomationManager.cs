using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Image;

namespace Amuse.App.Services
{
    public static class AutomationManager
    {
        /// <summary>
        /// Create diffusion automation jobs
        /// </summary>
        /// <param name="automationOptions">The automation options.</param>
        /// <param name="generateOptions">The diffusion options.</param>
        /// <param name="outputMediaType">Type of the output media.</param>
        /// <param name="inputMediaType">Type of the input media.</param>
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, GenerateInputOptions generateOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.Seed)
            {
                var seeds = GetSeeds(0, automationOptions.Count, generateOptions.Seed);
                foreach (var (index, seed) in seeds.Index())
                {
                    var options = generateOptions with { Seed = seed };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        GenerateOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, outputMediaType.GetLocalizedName(), seed)
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.PromptLines)
            {
                var promptLines = (await File.ReadAllLinesAsync(automationOptions.InputFile))
                    .Select((prompt, index) => new PromptLine(index + 1, prompt))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Prompt))
                    .ToArray();
                var seeds = GetSeeds(generateOptions.Seed, promptLines.Length);
                foreach (var (index, promptLine) in promptLines.Index())
                {
                    var seed = seeds[index];
                    var name = $"Line{promptLine.LineNumber}";
                    var options = generateOptions with
                    {
                        Seed = seed,
                        Prompt = $"{promptLine.Prompt} {generateOptions.Prompt}"
                    };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        GenerateOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.PromptFiles)
            {
                var promptFiles = new List<PromptFile>();
                foreach (var filename in GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.TextFileExtensions))
                {
                    var prompt = await File.ReadAllTextAsync(filename);
                    if (!string.IsNullOrWhiteSpace(prompt))
                        promptFiles.Add(new PromptFile(filename, prompt));
                }

                var seeds = GetSeeds(generateOptions.Seed, promptFiles.Count);
                foreach (var (index, promptFile) in promptFiles.Index())
                {
                    var seed = seeds[index];
                    var name = Path.GetFileNameWithoutExtension(promptFile.Filename);
                    var options = generateOptions with
                    {
                        Seed = seed,
                        Prompt = $"{promptFile.Prompt} {generateOptions.Prompt}"
                    };
                    yield return new AutomationJob
                    {
                        Id = index + 1,
                        Count = seeds.Length,
                        GenerateOptions = options,
                        OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
                    };
                }
            }
            else if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Image)
                {
                    var imageFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.ImageFileExtensions);
                    var seeds = GetSeeds(generateOptions.Seed, imageFiles.Length);
                    foreach (var (index, filename) in imageFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var image = await ImageInput.CreateAsync(filename);
                        var options = generateOptions with { Seed = seed };
                        if (automationOptions.UseInputSize)
                        {
                            options.Width = image.Width;
                            options.Height = image.Height;
                        }

                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            GenerateOptions = options,
                            InputImages = [image],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.VideoFileExtensions);
                    var seeds = GetSeeds(generateOptions.Seed, videoFiles.Length);
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            GenerateOptions = generateOptions with { Seed = seed },
                            VideoStreams = [videoStream],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
                        };
                    }
                }
                else if (inputMediaType == MediaType.Audio)
                {
                    var audioFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.AudioFileExtensions);
                    var seeds = GetSeeds(generateOptions.Seed, audioFiles.Length);
                    foreach (var (index, filename) in audioFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var audioStream = await AudioInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            GenerateOptions = generateOptions with { Seed = seed },
                            AudioStreams = [audioStream],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
                        };
                    }
                }
                else if (inputMediaType == MediaType.Text)
                {
                    var textFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.TextFileExtensions);
                    var seeds = GetSeeds(generateOptions.Seed, textFiles.Length);
                    foreach (var (index, filename) in textFiles.Index())
                    {
                        var seed = seeds[index];
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var textInput = await TextInput.CreateAsync(filename, System.Text.Encoding.UTF8);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = seeds.Length,
                            GenerateOptions = generateOptions with { Seed = seed },
                            InputTexts = [textInput],
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, name, seed)
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
                    var imageFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.ImageFileExtensions);
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
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"{AppDefault.Upscale}_{name}")
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.VideoFileExtensions);
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
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"{AppDefault.Upscale}_{name}")
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
                    var imageFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.ImageFileExtensions);
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
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"{AppDefault.Extract}_{name}")
                        };
                    }
                }
                else if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.VideoFileExtensions);
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
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"{AppDefault.Extract}_{name}")
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
        public static async IAsyncEnumerable<AutomationJob> CreateJobsAsync(AutomationOptions automationOptions, InterpolateInputOptions interpolateOptions, MediaType outputMediaType, MediaType inputMediaType)
        {
            if (automationOptions.Type == AutomationType.InputFiles)
            {
                if (inputMediaType == MediaType.Video)
                {
                    var videoFiles = GetFiles(automationOptions.InputDirectory, TensorStack.WPF.Common.VideoFileExtensions);
                    foreach (var (index, filename) in videoFiles.Index())
                    {
                        var name = Path.GetFileNameWithoutExtension(filename);
                        var videoStream = await VideoInputStream.CreateAsync(filename);
                        yield return new AutomationJob
                        {
                            Id = index + 1,
                            Count = videoFiles.Length,
                            VideoStreams = [videoStream],
                            InterpolateOptions = interpolateOptions with { },
                            OutputFile = GetOutputFile(automationOptions, outputMediaType, $"{AppDefault.Interpolate}_{name}")
                        };
                    }
                }
            }
        }


        /// <summary>
        /// Gets the files in the directory with one of the extensions, in name order.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="extensions">The file extensions, including the dot.</param>
        private static string[] GetFiles(string directory, string[] extensions)
        {
            return Directory.EnumerateFiles(directory, "*.*")
                .Where(x => extensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }


        /// <summary>
        /// Gets the output filename.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="fileId">The file identifier.</param>
        /// <param name="seed">The seed, appended to the filename when enabled.</param>
        private static string GetOutputFile(AutomationOptions options, MediaType mediaType, string fileId, int seed = 0)
        {
            if (!options.UseOutputDirectory)
                return null;

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                return null;

            if (!string.IsNullOrEmpty(options.OutputPrefix))
                fileId = $"{options.OutputPrefix}{fileId}";

            if (options.OutputPostFixSeed && seed > 0)
                fileId = $"{fileId}_{seed}";

            var ext = mediaType.GetExtension();
            var filename = $"{fileId}.{ext}";
            return Path.Combine(options.OutputDirectory, filename);
        }


        /// <summary>
        /// Gets the seeds.
        /// </summary>
        /// <param name="initialSeed">The initial seed, repeated for every job when set, otherwise random seeds are used.</param>
        /// <param name="count">The count.</param>
        /// <param name="baseSeed">The base seed of the random seeds, 0 for a random base.</param>
        private static int[] GetSeeds(int initialSeed, int count, int baseSeed = 0)
        {
            if (count <= 0)
                return [];

            if (initialSeed == 0)
            {
                baseSeed = baseSeed == 0 ? Random.Shared.Next() : baseSeed;
                var random = new Random(baseSeed);
                return [baseSeed, .. Enumerable.Range(0, count - 1).Select(i => random.Next())];
            }
            return Enumerable.Repeat(initialSeed, count).ToArray();
        }


        /// <summary>
        /// A line of a prompt file and its line number.
        /// </summary>
        private sealed record PromptLine(int LineNumber, string Prompt);


        /// <summary>
        /// A prompt file and its prompt.
        /// </summary>
        private sealed record PromptFile(string Filename, string Prompt);
    }
}
