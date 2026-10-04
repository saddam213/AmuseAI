
using Amuse.App.Common;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    public partial class TextControlBase : BaseControl
    {
        private readonly StreamingTextBuffer _streamBuffer;
        private readonly DispatcherTimer _streamUpdateTimer;
        private readonly StringBuilder _currentResultStream;
        private readonly ObservableCollection<ConversationModel> _conversation;
        private float _streamUpdateInterval = 60;
        private bool _isToolbarEnabled = true;
        private int _tokenCount;

        ///// <summary>
        /// Initializes a new instance of the <see cref="TextControlBase"/> class.
        /// </summary>
        public TextControlBase()
        {
            _streamBuffer = new StreamingTextBuffer();
            _currentResultStream = new StringBuilder();
            _conversation = new ObservableCollection<ConversationModel>();
            _streamUpdateTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(_streamUpdateInterval), DispatcherPriority.Background, OnStreamUpdate, Dispatcher);
            ClearCommand = new AsyncRelayCommand(ClearAsync, CanClear);
            Progress = new ProgressInfo();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(TextControlBase));
        public static readonly DependencyProperty IsSaveEnabledProperty = DependencyProperty.Register(nameof(IsSaveEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty IsRemoveEnabledProperty = DependencyProperty.Register(nameof(IsRemoveEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(ProgressInfo), typeof(TextControlBase), new PropertyMetadata(new ProgressInfo()));
        public static readonly DependencyProperty IsInputEnabledProperty = DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty IsContextMenuEnabledProperty = DependencyProperty.Register(nameof(IsContextMenuEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty IsMarkdownEnabledProperty = DependencyProperty.Register(nameof(IsMarkdownEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty IsStreamUpdateEnabledProperty = DependencyProperty.Register(nameof(IsStreamUpdateEnabled), typeof(bool), typeof(TextControlBase), new PropertyMetadata(true));
        public static readonly DependencyProperty IsThinkingVisibleProperty = DependencyProperty.Register(nameof(IsThinkingVisible), typeof(bool), typeof(TextControlBase), new PropertyMetadata(false));
        public static readonly DependencyProperty MaxTokenLengthProperty = DependencyProperty.Register(nameof(MaxTokenLength), typeof(int), typeof(TextControlBase), new PropertyMetadata(0));
        public event EventHandler OnConversationClear;
        public AsyncRelayCommand ClearCommand { get; }
        public int Count => _conversation?.Count ?? 0;
        public ConversationModel CurrentResult { get; protected set; }

        public ObservableCollection<ConversationModel> Conversation => _conversation;

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public bool IsSaveEnabled
        {
            get { return (bool)GetValue(IsSaveEnabledProperty); }
            set { SetValue(IsSaveEnabledProperty, value); }
        }

        public bool IsRemoveEnabled
        {
            get { return (bool)GetValue(IsRemoveEnabledProperty); }
            set { SetValue(IsRemoveEnabledProperty, value); }
        }

        public ProgressInfo Progress
        {
            get { return (ProgressInfo)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public bool IsInputEnabled
        {
            get { return (bool)GetValue(IsInputEnabledProperty); }
            set { SetValue(IsInputEnabledProperty, value); }
        }

        public bool IsContextMenuEnabled
        {
            get { return (bool)GetValue(IsContextMenuEnabledProperty); }
            set { SetValue(IsContextMenuEnabledProperty, value); }
        }

        public bool IsMarkdownEnabled
        {
            get { return (bool)GetValue(IsMarkdownEnabledProperty); }
            set { SetValue(IsMarkdownEnabledProperty, value); }
        }

        public bool IsStreamUpdateEnabled
        {
            get { return (bool)GetValue(IsStreamUpdateEnabledProperty); }
            set { SetValue(IsStreamUpdateEnabledProperty, value); }
        }

        public bool IsThinkingVisible
        {
            get { return (bool)GetValue(IsThinkingVisibleProperty); }
            set { SetValue(IsThinkingVisibleProperty, value); }
        }

        public int MaxTokenLength
        {
            get { return (int)GetValue(MaxTokenLengthProperty); }
            set { SetValue(MaxTokenLengthProperty, value); }
        }

        public bool IsToolbarEnabled
        {
            get { return _isToolbarEnabled; }
            set { SetProperty(ref _isToolbarEnabled, value); }
        }

        public int TokenCount
        {
            get { return _tokenCount; }
            set { SetProperty(ref _tokenCount, value); }
        }

        public float StreamUpdateInterval
        {
            get { return _streamUpdateInterval; }
            set
            {
                if (SetProperty(ref _streamUpdateInterval, value))
                {
                    _streamUpdateTimer.Interval = TimeSpan.FromMilliseconds(_streamUpdateInterval);
                }
            }
        }


        /// <summary>
        /// Resets the control
        /// </summary>
        public virtual Task ResetAsync()
        {
            TokenCount = 0;
            Progress.Clear();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Clears the control
        /// </summary>
        public virtual async Task ClearAsync()
        {
            await ResetAsync();
        }


        /// <summary>
        /// Determines whether this instance can clear.
        /// </summary>
        /// <returns><c>true</c> if this instance can clear; otherwise, <c>false</c>.</returns>
        protected virtual bool CanClear()
        {
            return _conversation?.Count > 0 || CurrentResult != null;
        }


        /// <summary>
        /// Handles the <see cref="E:StreamUpdate" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual async void OnStreamUpdate(object sender, EventArgs e)
        {
            NotifyPropertyChanged(nameof(TokenCount));
            Progress.Update(TokenCount, MaxTokenLength);
            if (IsStreamUpdateEnabled)
                await FlushStreamAsync();
        }


        /// <summary>
        /// Flush stream.
        /// </summary>
        protected virtual async Task FlushStreamAsync()
        {
            var buffer = _streamBuffer.Flush();
            if (string.IsNullOrEmpty(buffer))
                return;

            _currentResultStream.Append(buffer);
            await OnStreamFlushAsync(buffer);
        }


        /// <summary>
        /// Called when stream flush complete
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        protected virtual Task OnStreamFlushAsync(string buffer)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds a conversation message.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <param name="content">The content.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="audioIndex">Index of the audio.</param>
        protected virtual ConversationModel AddConversationMessage(ConversationRole role, string content, Dictionary<int, string> imageIndex = default, Dictionary<int, string> audioIndex = default, Dictionary<int, string> videoIndex = default)
        {
            var isUnclosed = RegexManager.HasUnclosedFence(content);
            var messageClose = GenerateCloseTag(isUnclosed);
            var message = new ConversationModel
            {
                Role = role,
                ImageIndex = imageIndex,
                AudioIndex = audioIndex,
                VideoIndex = videoIndex,
                Content = content + messageClose
            };

            if (role == ConversationRole.System)
            {
                _conversation.RemoveAll(x => x.Role == ConversationRole.System);
                _conversation.Insert(0, message);
            }
            else if (role == ConversationRole.Assistant)
            {
                message.Thinking = Utils.GetThinkingText(content);
                message.Content = Utils.GetResponseText(content) + messageClose;
                _conversation.Add(message);
            }
            else
            {
                _conversation.Add(message);
            }
            return message;
        }


        /// <summary>
        /// Resets the current result.
        /// </summary>
        protected void ResetCurrentResult()
        {
            CurrentResult = null;
            _currentResultStream.Clear();
        }


        /// <summary>
        /// Sets the current result.
        /// </summary>
        protected void SetCurrentResult(ConversationModel message)
        {
            CurrentResult = message;
            _currentResultStream.Clear();
            _currentResultStream.Append(message?.Content);
        }


        /// <summary>
        /// Updates the stream response.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="tokens">The tokens.</param>
        public virtual void UpdateStreamResponse(string token, int tokens)
        {
            _tokenCount = tokens;
            _streamBuffer.Append(token);
        }


        /// <summary>
        /// Clears the conversation
        /// </summary>
        public virtual Task ClearConversationAsync()
        {
            ResetCurrentResult();

            Conversation.Clear();
            OnConversationClear?.Invoke(this, new EventArgs());
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds the system prompt asynchronous.
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        public virtual Task AddSystemPromptAsync(string prompt)
        {
            ResetCurrentResult();

            if (string.IsNullOrEmpty(prompt))
                return Task.CompletedTask;

            AddConversationMessage(ConversationRole.System, prompt);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds the user prompt
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="audioIndex">Index of the audio.</param>
        public virtual Task AddUserPromptAsync(string prompt, Dictionary<int, string> imageIndex = default, Dictionary<int, string> audioIndex = default, Dictionary<int, string> videoIndex = default)
        {
            ResetCurrentResult();

            AddConversationMessage(ConversationRole.User, prompt, imageIndex, audioIndex, videoIndex);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds the assistant response
        /// </summary>
        /// <param name="response">The response.</param>
        public virtual Task AddAssistantResponseAsync(string response)
        {
            ResetCurrentResult();

            var message = AddConversationMessage(ConversationRole.Assistant, response);
            CurrentResult = message;
            return Task.CompletedTask;
        }


        /// <summary>
        /// End stream response
        /// </summary>
        public virtual async Task<string> EndStreamResponseAsync()
        {
            await Task.Delay(150);
            await FlushStreamAsync();
            if (_currentResultStream.Length == 0)
                return null;

            var response = _currentResultStream.ToString();
            var message = AddConversationMessage(ConversationRole.Assistant, response);
            CurrentResult = message;
            _currentResultStream.Clear();
            return response;
        }


        /// <summary>
        /// Generates the close tag.
        /// </summary>
        /// <param name="isUnclosed">if set to <c>true</c> [is unclosed].</param>
        private static string GenerateCloseTag(bool isUnclosed)
        {
            const string unclosed = $"\n```\n";
            return isUnclosed ? unclosed : string.Empty;
        }
    }
}
