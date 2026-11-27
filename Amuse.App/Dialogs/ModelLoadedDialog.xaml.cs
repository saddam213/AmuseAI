using Amuse.App.Resources;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Asks what to do with a model that is already loaded before another model is loaded next to it.
    /// </summary>
    public sealed partial class ModelLoadedDialog : DialogControl
    {
        private string _messageBody;
        private ModelLoadedResult _result;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelLoadedDialog"/> class.
        /// </summary>
        public ModelLoadedDialog()
        {
            UnloadCommand = new AsyncRelayCommand(UnloadAsync);
            ContinueCommand = new AsyncRelayCommand(ContinueAsync);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            InitializeComponent();
        }

        public AsyncRelayCommand UnloadCommand { get; }
        public AsyncRelayCommand ContinueCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public string MessageBody
        {
            get { return _messageBody; }
            set { SetProperty(ref _messageBody, value); }
        }


        /// <summary>
        /// Shows the dialog for the loaded model.
        /// </summary>
        /// <param name="message">The message, naming the loaded model and what loading the new model does with it.</param>
        public async Task<ModelLoadedResult> ShowDialogAsync(string message)
        {
            _result = ModelLoadedResult.Cancel;
            Title = AppDefault.ModelLoadedTitle;
            MessageBody = message;
            await base.ShowDialogAsync();
            return _result;
        }


        /// <summary>
        /// Closes the dialog with the unload result.
        /// </summary>
        private Task UnloadAsync()
        {
            _result = ModelLoadedResult.Unload;
            return base.SaveAsync();
        }


        /// <summary>
        /// Closes the dialog with the continue result.
        /// </summary>
        private Task ContinueAsync()
        {
            _result = ModelLoadedResult.Continue;
            return base.SaveAsync();
        }
    }


    public enum ModelLoadedResult
    {
        Cancel = 0,
        Continue = 1,
        Unload = 2
    }
}
