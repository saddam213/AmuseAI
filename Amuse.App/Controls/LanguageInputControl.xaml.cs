using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for LanguageInputControl.xaml
    /// </summary>
    public partial class LanguageInputControl : BaseControl
    {
        private InputTabOption _selectedOption;

        public LanguageInputControl()
        {
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            ToolCalls = new ObservableCollection<ToolCallModel>();
            ToolEnableCommand = new AsyncRelayCommand<bool>(ToolEnableAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(LanguageInputControl), new PropertyMetadata<LanguageInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(GenerateInputOptions), typeof(LanguageInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(LanguageInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(LanguageInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(LanguageInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(LanguageInputControl));
        public static readonly DependencyProperty IsInputEnabledProperty = DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool), typeof(LanguageInputControl), new PropertyMetadata(true));
        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(LanguageInputControl), new PropertyMetadata(false));
        public static readonly DependencyProperty ToolServiceProperty = DependencyProperty.Register(nameof(ToolService), typeof(IToolService), typeof(LanguageInputControl));

        public View ViewType { get; set; }
        public ProcessType ProcessType { get; set; }
        public RelayCommand<bool> SeedCommand { get; }
        public AsyncRelayCommand<bool> ToolEnableCommand { get; }
        public ObservableCollection<ToolCallModel> ToolCalls { get; }

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

        public IToolService ToolService
        {
            get { return (ToolService)GetValue(ToolServiceProperty); }
            set { SetValue(ToolServiceProperty, value); }
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

        public bool IsInputEnabled
        {
            get { return (bool)GetValue(IsInputEnabledProperty); }
            set { SetValue(IsInputEnabledProperty, value); }
        }

        public bool IsControlBusy
        {
            get { return (bool)GetValue(IsControlBusyProperty); }
            set { SetValue(IsControlBusyProperty, value); }
        }

        public InputTabOption SelectedOption
        {
            get { return _selectedOption; }
            set { SetProperty(ref _selectedOption, value); }
        }

        private bool _isPromptEnabled;
        private bool _isInputImagesEnabled;

        public bool IsPromptEnabled
        {
            get { return _isPromptEnabled; }
            set { SetProperty(ref _isPromptEnabled, value); }
        }

        public bool IsInputImagesEnabled
        {
            get { return _isInputImagesEnabled; }
            set { SetProperty(ref _isInputImagesEnabled, value); }
        }


        public string DefaultPrompt { get; set; }

        private Task OnPipelineChanged(PipelineModel oldPipeline, PipelineModel newPipeline)
        {
            if (newPipeline is null || newPipeline.LanguageModel is null)
            {
                return Task.CompletedTask;
            }

            var oldModel = oldPipeline?.LanguageModel;
            var oldOptions = oldModel?.DefaultOptions;
            var newModel = newPipeline?.LanguageModel;
            var newOptions = newModel?.DefaultOptions;

            var previousOptions = Options;
            Options = new GenerateInputOptions
            {
                // Keep
                Seed = previousOptions?.Seed ?? 0,
                Prompt = previousOptions?.Prompt ?? DefaultPrompt,
                IsThinkingEnabled = previousOptions?.IsThinkingEnabled ?? newOptions?.IsThinkingEnabled ?? false,

                MinLength = newOptions.MinLength,
                MaxLength = newOptions.MaxLength,
                IsSamplingEnabled = newOptions.IsSamplingEnabled,
                Beams = newOptions.Beams,
                Temperature = newOptions.Temperature,
                TopK = newOptions.TopK,
                TopP = newOptions.TopP,
                TopH = newOptions.TopH,
                TypicalP = newOptions.TypicalP,
                RepetitionPenalty = newOptions.RepetitionPenalty,
                LengthPenalty = newOptions.LengthPenalty,
                NoRepeatNgramSize = newOptions.NoRepeatNgramSize,
                EarlyStopping = newOptions.EarlyStopping,
                ChunkSize = newOptions.ChunkSize,
                CacheType = newOptions.CacheType,
            };

            // Automation
            var automationType = AutomationOptions.GetSupportedTypes(ViewType);
            AutomationOptions = new AutomationOptions
            {
                ViewType = ViewType,
                Type = automationType.FirstOrDefault(),
                OutputPrefix = string.Empty,
                OutputPostFixSeed = false
            };

            // Context
            ContextControlElement.TextMaxCount = 10000;
            ContextControlElement.ImageMaxCount = newOptions.InputImageMaxCount;
            ContextControlElement.AudioMaxCount = newOptions.InputAudioMaxCount;
            ContextControlElement.VideoMaxCount = newOptions.InputVideoMaxCount;

            // ToolCalls
            PopulateToolCalls();
            return Task.CompletedTask;
        }


        private void GenerateSeed(bool random)
        {
            Options.Seed = random ? 0 : Random.Shared.Next();
        }


        public void EndConversation()
        {
            ContextControlElement.ReleaseContext();
        }


        public PromptInputs GetPromptInputs(string prompt)
        {
            return ContextControlElement.GetPromptInputs(prompt);
        }


        public PromptInputs GetPromptInputs(string prompt, Collection<ConversationModel> conversation)
        {
            return ContextControlElement.GetPromptInputs(prompt, conversation);
        }


        public async Task CreateContextAsync(Collection<ConversationModel> conversation)
        {
            await ContextControlElement.CreateFromConversation(conversation);
        }


        private void PopulateToolCalls()
        {
            if (ToolCalls.IsNullOrEmpty())
            {
                foreach (var tool in ToolService.ToolDefinitions.OrderBy(x => x.DisplayOrder))
                {
                    ToolCalls.Add(new ToolCallModel(tool));
                }
            }
            ToolEnableAsync(true);
        }


        private Task ToolEnableAsync(bool _)
        {
            Options.SelectedTools.Clear();
            if (Options.IsToolCallsEnabled)
            {
                foreach (var toolCall in ToolCalls.Where(x => x.IsEnabled))
                {
                    Options.SelectedTools.Add(toolCall.Name);
                }
            }
            return Task.CompletedTask;
        }
    }
}
