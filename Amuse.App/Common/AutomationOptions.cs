using Amuse.App.Views;
using System.IO;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class AutomationOptions : BaseModel
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


        public bool IsValid()
        {
            switch (Type)
            {
                case AutomationType.Seed:
                    return (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.PromptLines:
                    return Directory.Exists(_inputFile) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.PromptFiles:
                    return Directory.Exists(_inputDirectory) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                case AutomationType.InputFiles:
                    return Directory.Exists(_inputDirectory) && (UseOutputDirectory == false || Directory.Exists(_outputDirectory));
                default:
                    break;
            }
            return false;
        }


        private AutomationType[] GetSupportedTypes(View viewType)
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
                    return [AutomationType.Seed, AutomationType.PromptLines, AutomationType.PromptFiles];
                case View.AudioToText:
                    return [AutomationType.Seed, AutomationType.InputFiles];
                default:
                    break;
            }
            return [];
        }
    }
}