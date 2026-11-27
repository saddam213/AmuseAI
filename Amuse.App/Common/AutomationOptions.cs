using Amuse.App.Views;
using System.IO;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class AutomationOptions : BaseModel
    {
        private AutomationType _type;
        private AutomationType[] _types;
        private bool _useOutputDirectory;
        private bool _useInputSize = true;
        private string _outputDirectory;
        private string _inputDirectory;
        private string _inputFile;
        private bool _isHistoryEnabled = true;
        private int _count = 4;
        private View _viewType;
        private string _outputPrefix = "Diffusion_";
        private bool _outputPostFixSeed = true;

        public View ViewType
        {
            get { return _viewType; }
            init
            {
                SetProperty(ref _viewType, value);
                _types = GetSupportedTypes(ViewType);
                NotifyPropertyChanged(nameof(Types));
            }
        }

        public AutomationType[] Types => _types;

        public AutomationType Type
        {
            get { return _type; }
            set { SetProperty(ref _type, value); }
        }

        public int Count
        {
            get { return _count; }
            set { SetProperty(ref _count, value); }
        }

        public bool IsHistoryEnabled
        {
            get { return _isHistoryEnabled; }
            set { SetProperty(ref _isHistoryEnabled, value); }
        }

        public string InputFile
        {
            get { return _inputFile; }
            set { SetProperty(ref _inputFile, value); }
        }

        public string InputDirectory
        {
            get { return _inputDirectory; }
            set { SetProperty(ref _inputDirectory, value); }
        }

        public string OutputDirectory
        {
            get { return _outputDirectory; }
            set { SetProperty(ref _outputDirectory, value); }
        }

        public bool UseInputSize
        {
            get { return _useInputSize; }
            set { SetProperty(ref _useInputSize, value); }
        }

        public bool UseOutputDirectory
        {
            get { return _useOutputDirectory; }
            set { SetProperty(ref _useOutputDirectory, value); }
        }

        public string OutputPrefix
        {
            get { return _outputPrefix; }
            set { SetProperty(ref _outputPrefix, value); }
        }

        public bool OutputPostFixSeed
        {
            get { return _outputPostFixSeed; }
            set { SetProperty(ref _outputPostFixSeed, value); }
        }


        public bool IsValid()
        {
            switch (Type)
            {
                case AutomationType.Seed:
                    return _count > 0 && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.PromptLines:
                    return File.Exists(_inputFile) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.PromptFiles:
                    return Directory.Exists(_inputDirectory) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.InputFiles:
                    return Directory.Exists(_inputDirectory) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                default:
                    break;
            }
            return false;
        }


        public static AutomationType[] GetSupportedTypes(View viewType)
        {
            switch (viewType)
            {
                case View.TextToImage:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.ImageToImage:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles, AutomationType.InputFiles];
                case View.ImageEdit:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles, AutomationType.InputFiles];
                case View.ImageInpaint:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.PaintToImage:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.TextToVideo:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.ImageToVideo:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles, AutomationType.InputFiles];
                case View.VideoToVideo:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.FrameToFrame:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.MediaToVideo:
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];

                case View.ImageUpscale:
                    return [AutomationType.InputFiles];
                case View.ImageExtract:
                    return [AutomationType.InputFiles];
                case View.VideoUpscale:
                    return [AutomationType.InputFiles];
                case View.VideoExtract:
                    return [AutomationType.InputFiles];
                case View.VideoInterpolate:
                    return [AutomationType.InputFiles];

                case View.TextToAudio:
                    return [AutomationType.Seed, AutomationType.InputFiles];
                case View.TextToMusic:
                    return [AutomationType.Seed];
                case View.AudioToMusic:
                    return [AutomationType.Seed];
                case View.VoiceClone:
                    return [AutomationType.Seed, AutomationType.InputFiles];
                case View.AudioToText:
                    return [AutomationType.Seed, AutomationType.InputFiles];

                case View.ImageToText:
                    return [AutomationType.InputFiles];
                case View.TextInstruct:
                    return [AutomationType.PromptLines, AutomationType.PromptFiles];
                default:
                    break;
            }
            return [];
        }
    }
}