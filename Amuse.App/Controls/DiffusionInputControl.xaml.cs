using Amuse.App.Common;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for DiffusionInputControl.xaml
    /// </summary>
    public partial class DiffusionInputControl : BaseControl
    {
        private bool _isResolutionEnabled = true;
        private SizeOption _selectedResolution;
        private bool _isImageInputEnabled;
        private bool _isControlNetEnabled;
        private bool _isModelOptionsVisible;
        private bool _isSteps2Enabled;
        private bool _isGuidance2Enabled;
        private SamplerInputOptions[] _samplers;
        private bool _isImageControlNetSupported;
        private bool _isImageToImageControlNetSupported;
        private InputTabOption _selectedOption;

        public DiffusionInputControl()
        {
            LatentTileSizes = [16, 32, 64, 96, 128, 160, 192, 224, 256];
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            AddTriggerWordCommand = new AsyncRelayCommand<string>(AddTriggerWordAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(DiffusionInputControl), new PropertyMetadata<DiffusionInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(GenerateInputOptions), typeof(DiffusionInputControl));
        public static readonly DependencyProperty UpscaleOptionsProperty = DependencyProperty.Register(nameof(UpscaleOptions), typeof(UpscaleInputOptions), typeof(DiffusionInputControl));
        public static readonly DependencyProperty ExtractOptionsProperty = DependencyProperty.Register(nameof(ExtractOptions), typeof(ExtractInputOptions), typeof(DiffusionInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(DiffusionInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(DiffusionInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(DiffusionInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(DiffusionInputControl));

        public View ViewType { get; set; }
        public ProcessType ProcessType { get; set; }
        public RelayCommand<bool> SeedCommand { get; }
        public AsyncRelayCommand<string> AddTriggerWordCommand { get; }
        public List<int> LatentTileSizes { get; }

        public PipelineModel Pipeline
        {
            get { return (PipelineModel)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }

        public GenerateInputOptions Options
        {
            get { return (GenerateInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public UpscaleInputOptions UpscaleOptions
        {
            get { return (UpscaleInputOptions)GetValue(UpscaleOptionsProperty); }
            set { SetValue(UpscaleOptionsProperty, value); }
        }

        public ExtractInputOptions ExtractOptions
        {
            get { return (ExtractInputOptions)GetValue(ExtractOptionsProperty); }
            set { SetValue(ExtractOptionsProperty, value); }
        }

        public AutomationOptions AutomationOptions
        {
            get { return (AutomationOptions)GetValue(AutomationOptionsProperty); }
            set { SetValue(AutomationOptionsProperty, value); }
        }

        public ProgressInfo AutomationProgress
        {
            get { return (ProgressInfo)GetValue(AutomationProgressProperty); }
            set { SetValue(AutomationProgressProperty, value); }
        }

        public bool IsExecuting
        {
            get { return (bool)GetValue(IsExecutingProperty); }
            set { SetValue(IsExecutingProperty, value); }
        }

        public bool IsAutomating
        {
            get { return (bool)GetValue(IsAutomatingProperty); }
            set { SetValue(IsAutomatingProperty, value); }
        }

        public bool IsImageInputEnabled
        {
            get { return _isImageInputEnabled; }
            set { SetProperty(ref _isImageInputEnabled, value); }
        }

        public bool IsControlNetEnabled
        {
            get { return _isControlNetEnabled; }
            set { SetProperty(ref _isControlNetEnabled, value); }
        }

        public bool IsResolutionEnabled
        {
            get { return _isResolutionEnabled; }
            set
            {
                SetProperty(ref _isResolutionEnabled, value);
                if (_isResolutionEnabled)
                {
                    SelectedResolution = Pipeline?.DiffusionModel.Resolutions.FirstOrDefault(x => x.IsDefault);
                }
            }
        }

        public SizeOption SelectedResolution
        {
            get { return _selectedResolution; }
            set
            {
                SetProperty(ref _selectedResolution, value);
                if (_selectedResolution != null && Options != null)
                {
                    Options.Width = _selectedResolution.Width;
                    Options.Height = _selectedResolution.Height;
                }
            }
        }

        public bool IsModelOptionsVisible
        {
            get { return _isModelOptionsVisible; }
            set { SetProperty(ref _isModelOptionsVisible, value); }
        }

        public bool IsSteps2Enabled
        {
            get { return _isSteps2Enabled; }
            set { SetProperty(ref _isSteps2Enabled, value); }
        }

        public bool IsGuidance2Enabled
        {
            get { return _isGuidance2Enabled; }
            set { SetProperty(ref _isGuidance2Enabled, value); }
        }

        public SamplerInputOptions[] Samplers
        {
            get { return _samplers; }
            set { SetProperty(ref _samplers, value); }
        }

        public bool IsImageControlNetSupported
        {
            get { return _isImageControlNetSupported; }
            set { SetProperty(ref _isImageControlNetSupported, value); }
        }

        public bool IsImageToImageControlNetSupported
        {
            get { return _isImageToImageControlNetSupported; }
            set { SetProperty(ref _isImageToImageControlNetSupported, value); }
        }

        public InputTabOption SelectedOption
        {
            get { return _selectedOption; }
            set { SetProperty(ref _selectedOption, value); }
        }


        private Task OnPipelineChanged(PipelineModel oldPipeline, PipelineModel newPipeline)
        {
            if (newPipeline is null || newPipeline.DiffusionModel is null)
            {
                IsModelOptionsVisible = false;
                return Task.CompletedTask;
            }

            var oldModel = oldPipeline?.DiffusionModel;
            var oldOptions = oldModel?.DefaultOptions;
            var newModel = newPipeline?.DiffusionModel;
            var newOptions = newModel?.DefaultOptions;

            if (oldModel == newModel)
            {
                Options.LoraOptions = CreateLoraOptions(newPipeline, Options.LoraOptions);
                if (oldPipeline?.ControlNetModel != newPipeline.ControlNetModel)
                    Options.ControlType = newPipeline.ControlNetModel?.ControlType ?? ControlNetType.None;

                return Task.CompletedTask;
            }

            // UI Flags
            IsSteps2Enabled = newOptions.Steps2 > 0;
            IsGuidance2Enabled = newOptions.GuidanceScale2 > 0;
            IsModelOptionsVisible = newPipeline.UpscaleModel is not null || newPipeline.ExtractModel is not null;
            IsImageControlNetSupported = newPipeline.DiffusionModel.ProcessTypes.Contains(ProcessType.ImageControlNet);
            IsImageToImageControlNetSupported = newPipeline.DiffusionModel.ProcessTypes.Contains(ProcessType.ImageToImageControlNet);

            var previousOptions = Options;
            Options = new GenerateInputOptions
            {
                // Keep
                Prompt = previousOptions?.Prompt,
                NegativePrompt = previousOptions?.NegativePrompt,
                Seed = previousOptions?.Seed ?? 0,
                LoraOptions = CreateLoraOptions(newPipeline, previousOptions?.LoraOptions),
                Strength = ProcessType == ProcessType.ImageToImage && !IsImageControlNetSupported ? (previousOptions?.Strength ?? 0.7f) : 1f,
                ControlNetStrength = IsImageControlNetSupported ? (previousOptions?.ControlNetStrength ?? 0.7f) : 1f,
                ControlType = newPipeline.ControlNetModel?.ControlType ?? ControlNetType.None,
                IsSource2Enabled = ProcessType == ProcessType.ImageToVideo ? (previousOptions?.IsSource2Enabled ?? false) && newOptions.IsFirstFrameLastFrameEnabled : (previousOptions?.IsSource2Enabled ?? false),

                // Update
                Steps = newOptions.Steps,
                Steps2 = newOptions.Steps2,
                GuidanceScale = newOptions.GuidanceScale,
                GuidanceScale2 = newOptions.GuidanceScale2,
                Frames = newOptions.Frames,
                FrameRate = newOptions.FrameRate,
                FrameChunk = newOptions.FrameChunk,
                FrameChunkOverlap = newOptions.FrameChunkOverlap,
                NoiseCondition = newOptions.NoiseCondition,
                IsVaeTilingEnabled = newOptions.IsVaeTilingEnabled,
                IsVaeSlicingEnabled = newOptions.IsVaeSlicingEnabled,
                ClipSkip = newOptions.ClipSkip,
                MoeBoundary = newOptions.MoeBoundary,

                LatentUpscale = LatentUpscale.None,
                LatentUpscaleSteps = newOptions.LatentUpscaleSteps,
                LatentUpscaleTileSize = newOptions.LatentUpscaleTileSize,
                LatentUpscaleStrength = newOptions.LatentUpscaleStrength,
            };

            //Resolution
            SelectedResolution = newModel?.Resolutions.FirstOrDefault(x => x.Width == _selectedResolution?.Width && x.Height == _selectedResolution?.Height)
                              ?? newModel?.Resolutions.OrderByDescending(x => x.IsDefault).FirstOrDefault();

            //Samplers
            Samplers = newOptions.Samplers.Copy();
            Options.SamplerOptions = Samplers.FirstOrDefault(x => x.Sampler == newOptions.Sampler);

            // Automation
            AutomationOptions = new AutomationOptions
            {
                ViewType = ViewType
            };

            return Task.CompletedTask;
        }


        /// <summary>
        /// Creates the LoRA options for the pipeline, keeping the strength of adapters that are still selected.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="previousOptions">The previous LoRA options.</param>
        private static List<LoraOptionModel> CreateLoraOptions(PipelineModel pipeline, List<LoraOptionModel> previousOptions)
        {
            return pipeline.LoraAdapterModel?
                .Select(x => new LoraOptionModel
                {
                    Name = x.Name,
                    Key = x.Key,
                    Strength = previousOptions?.FirstOrDefault(p => p.Key == x.Key)?.Strength ?? 1f
                })
                .ToList();
        }


        private void GenerateSeed(bool random)
        {
            Options.Seed = random ? 0 : Random.Shared.Next();
        }


        private Task AddTriggerWordAsync(string triggerWord)
        {
            if (string.IsNullOrEmpty(Options.Prompt))
            {
                Options.Prompt = triggerWord;
            }
            else
            {
                Options.Prompt += $", {triggerWord}";
            }
            return Task.CompletedTask;
        }

    }
}
