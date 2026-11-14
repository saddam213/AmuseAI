using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for TextResultControl.xaml
    /// </summary>
    public partial class TextResultControl : TextControlBase
    {
        private List<TextInput> _beamResults;
        private TextInput _selectedBeamResult;

        ///// <summary>
        /// Initializes a new instance of the <see cref="TextResultControl"/> class.
        /// </summary>
        public TextResultControl()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => SelectedBeamResult is not null);
            CopyCommand = new AsyncRelayCommand<bool>(CopyAsync, (x) => SelectedBeamResult is not null);
            InitializeComponent();
        }

        public static readonly DependencyProperty IsMultipleBeamResultProperty = DependencyProperty.Register(nameof(IsMultipleBeamResult), typeof(bool), typeof(TextResultControl), new PropertyMetadata(false));
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand<bool> CopyCommand { get; }

        public bool IsMultipleBeamResult
        {
            get { return (bool)GetValue(IsMultipleBeamResultProperty); }
            set { SetValue(IsMultipleBeamResultProperty, value); }
        }

        public List<TextInput> BeamResults
        {
            get { return _beamResults; }
            set { SetProperty(ref _beamResults, value); }
        }

        public TextInput SelectedBeamResult
        {
            get { return _selectedBeamResult; }
            set { SetProperty(ref _selectedBeamResult, value); }
        }


        /// <summary>
        /// Adds the beam results.
        /// </summary>
        /// <param name="results">The results.</param>
        public void AddBeamResults(List<TextInput> results)
        {
            if (IsMultipleBeamResult)
                BeamResults = results;
            SelectedBeamResult = results?.FirstOrDefault();
        }


        /// <summary>
        /// Resets the control
        /// </summary>
        public override async Task ResetAsync()
        {
            await base.ResetAsync();
            var tabMarkdownElement = GetBeamMarkdownElement();
            await ClearConversationAsync();
            BeamResults = null;
            SelectedBeamResult = null;
            if (tabMarkdownElement != null)
                await tabMarkdownElement.CloseAsync();
        }


        /// <summary>
        /// Clears thes control
        /// </summary>
        public override async Task ClearAsync()
        {
            await base.ClearAsync();
            await ResultControl.CloseAsync();
        }


        /// <summary>
        /// Clear conversation
        /// </summary>
        public override async Task ClearConversationAsync()
        {
            await base.ClearConversationAsync();
            await ResultControl.ClearAsync();
        }


        /// <summary>
        /// Add assistant response
        /// </summary>
        /// <param name="response">The response.</param>
        public override async Task AddAssistantResponseAsync(string response)
        {
            await base.AddAssistantResponseAsync(response);
            await ResultControl.AppendTextAsync(CurrentResult.Content);
        }


        /// <summary>
        /// End the stream response 
        /// </summary>
        public override async Task<string> EndStreamResponseAsync()
        {
            var response = await base.EndStreamResponseAsync();
            if (response == null)
                return response;

            await ResultControl.AppendTextAsync("\n");
            return response;
        }


        /// <summary>
        /// Updates the stream response.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="tokens">The tokens.</param>
        public override void UpdateStreamResponse(string token, int tokens)
        {
            if (IsMultipleBeamResult)
                return;
            base.UpdateStreamResponse(token, tokens);
        }


        /// <summary>
        /// On stream flush
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        protected override async Task OnStreamFlushAsync(string buffer)
        {
            await base.OnStreamFlushAsync(buffer);
            await ResultControl.AppendStreamAsync(buffer);
        }


        /// <summary>
        /// Invoked when an unhandled <see cref="E:System.Windows.Input.Mouse.MouseEnter" /> attached event is raised on this element. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.MouseEventArgs" /> that contains the event data.</param>
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            if (!IsKeyboardFocusWithin)
                Focus();
        }


        /// <summary>
        /// Copies the Response Text
        /// </summary>
        private async Task CopyAsync(bool formatted)
        {
            var selectedElement = GetSelectedMarkdownElement();
            if (selectedElement == null)
                return;

            await selectedElement.CopyResponseAsync(formatted);
        }


        /// <summary>
        /// Save the Response Text to file
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task SaveAsync()
        {
            var selectedElement = GetSelectedMarkdownElement();
            if (selectedElement == null)
                return;

            await selectedElement.SaveAsync(false);
        }


        /// <summary>
        /// Gets the selected element.
        /// </summary>
        private MarkdownElement GetSelectedMarkdownElement()
        {
            if (IsMultipleBeamResult)
                return GetBeamMarkdownElement();
            return ResultControl;
        }


        /// <summary>
        /// Gets the Beam markdown element.
        /// </summary>
        /// <returns>MarkdownElement.</returns>
        private MarkdownElement GetBeamMarkdownElement()
        {
            return ResultTabControl.FindVisualChildren<MarkdownElement>().FirstOrDefault();
        }

    }
}
