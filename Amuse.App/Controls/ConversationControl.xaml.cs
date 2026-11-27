
using Amuse.App.Common;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for ConversationControl.xaml
    /// </summary>
    public partial class ConversationControl : TextControlBase
    {
        ///// <summary>
        /// Initializes a new instance of the <see cref="ConversationControl"/> class.
        /// </summary>
        public ConversationControl()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CurrentResult is not null);
            CopyCommand = new AsyncRelayCommand<bool>(CopyAsync, (f) => CurrentResult is not null);
            RewindConversationCommand = new AsyncRelayCommand(RewindConversationAsync, () => Count > 2);
            BranchConversationCommand = new AsyncRelayCommand(BranchConversationAsync, () => Count > 0);
            InitializeComponent();
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand<bool> CopyCommand { get; }
        public AsyncRelayCommand RewindConversationCommand { get; }
        public AsyncRelayCommand BranchConversationCommand { get; }
        public event EventHandler OnConversationBranch;
        public event EventHandler<string> OnConversationLoaded;

        /// <summary>
        /// Resets the control
        /// </summary>
        public override async Task ResetAsync()
        {
            await base.ResetAsync();
            await ClearConversationAsync();
        }


        /// <summary>
        /// Clears thes control
        /// </summary>
        public override async Task ClearAsync()
        {
            await base.ClearAsync();
            ResetCurrentResult();
            await ResultControl.CloseAsync();
        }


        /// <summary>
        /// Gets the conversation markdown.
        /// </summary>
        public string GetConversationMarkdown()
        {
            return ResultControl.GetPlainText();
        }


        /// <summary>
        /// Load conversation
        /// </summary>
        /// <param name="conversation">The conversation.</param>
        public async Task LoadConversationAsync(IEnumerable<ConversationModel> conversation)
        {
            if (conversation.IsNullOrEmpty())
                return;

            var markdownConversation = new StringBuilder();
            foreach (var message in conversation)
            {
                var markdown = GenerateMarkdown(message);
                markdownConversation.AppendLine(markdown);
            }
            await ResultControl.SetTextAsync(markdownConversation.ToString());
        }


        /// <summary>
        /// Reload conversation
        /// </summary>
        public async Task ReloadConversationAsync()
        {
            await LoadConversationAsync(Conversation);
        }


        /// <summary>
        /// Clears the conversation
        /// </summary>
        public override async Task ClearConversationAsync()
        {
            await base.ClearConversationAsync();
            await ResultControl.ClearAsync();
        }


        /// <summary>
        /// Adds the system prompt asynchronous.
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        public override async Task AddSystemPromptAsync(string prompt)
        {
            ResetCurrentResult();

            var existingSystemPrompt = Conversation.FirstOrDefault(x => x.Role == ConversationRole.System);
            if (existingSystemPrompt == null)
            {
                if (string.IsNullOrEmpty(prompt))
                    return;

                AddConversationMessage(ConversationRole.System, prompt);
                await ReloadConversationAsync();
            }
            else
            {
                if (string.IsNullOrEmpty(prompt))
                {
                    Conversation.Remove(existingSystemPrompt);
                    await ReloadConversationAsync();
                }
                else if (existingSystemPrompt.Content != prompt)
                {
                    existingSystemPrompt.Content = prompt;
                    await ReloadConversationAsync();
                }
            }
        }


        /// <summary>
        /// Adds the user prompt
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="audioIndex">Index of the audio.</param>
        public override async Task AddUserPromptAsync(string prompt, Dictionary<int, string> imageIndex = default, Dictionary<int, string> audioIndex = default, Dictionary<int, string> videoIndex = default)
        {
            ResetCurrentResult();

            var message = AddConversationMessage(ConversationRole.User, prompt, imageIndex, audioIndex, videoIndex);
            var markdown = GenerateMarkdown(message, ConversationRole.Assistant);
            await ResultControl.AppendTextAsync(markdown);
        }


        /// <summary>
        /// Adds the assistant response
        /// </summary>
        /// <param name="response">The response.</param>
        public override async Task AddAssistantResponseAsync(string response)
        {
            await base.AddAssistantResponseAsync(response);
            var markdown = GenerateMarkdown(CurrentResult);
            await ResultControl.AppendTextAsync(markdown + '\n');
        }


        /// <summary>
        /// End stream response
        /// </summary>
        public override async Task<string> EndStreamResponseAsync()
        {
            var response = await base.EndStreamResponseAsync();
            if (response == null)
                return response;

            var isUnclosed = RegexManager.HasUnclosedFence(response);
            var markdownClose = AssistantCloseTag(isUnclosed);
            await ResultControl.AppendTextAsync(markdownClose);
            return response;
        }


        /// <summary>
        /// Called when stream flush complete
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
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
            await ResultControl.CopyResponseAsync(formatted);
        }


        /// <summary>
        /// Save the Response Text to file
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task SaveAsync()
        {
            await ResultControl.SaveAsync(false);
        }


        private async Task RewindConversationAsync()
        {
            Conversation.RemoveAt(Conversation.Count - 1);
            Conversation.RemoveAt(Conversation.Count - 1);
            await ReloadConversationAsync();
        }


        private Task BranchConversationAsync()
        {
            OnConversationBranch?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }



        /// <summary>
        /// Invoked when an unhandled <see cref="E:System.Windows.DragDrop.DragEnter" /> attached event reaches an element
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.DragEventArgs" /> that contains the event data.</param>
        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (!IsInputEnabled)
                return;

            try
            {
                Progress?.Indeterminate();
                var conversationFile = e.GetFileDrop();
                if (conversationFile?.Exists == true)
                {
                    var historyFilename = conversationFile.FullName.Replace(".txt", ".json");
                    var diffusionHistory = await Json.LoadAsync<DiffusionHistory>(historyFilename);
                    if (diffusionHistory != null)
                    {
                        Conversation.Clear();
                        foreach (var message in diffusionHistory.Options.Conversation)
                        {
                            Conversation.Add(message);
                        }
                        await ReloadConversationAsync();
                        OnConversationLoaded?.Invoke(this, diffusionHistory.Id);
                        SetCurrentResult(Conversation.LastOrDefault());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationControl] [Exception] OnDrop: {ex.Message}");
            }
            finally
            {
                Progress?.Clear();
            }
        }


        private static string AssistantCloseTag(bool isUnclosed)
        {
            const string assistantClosed = $"\n</assistant>\n";
            const string assistantUnclosed = $"\n```\n</assistant>\n";
            return isUnclosed ? assistantUnclosed : assistantClosed;
        }


        private static string GenerateMarkdown(ConversationModel message, ConversationRole? nextRole = null)
        {
            const string user = "<user>\n{0}\n</user>{1}";
            const string system = "<system>\n{0}\n</system>{1}";
            const string assistant = "<assistant>\n{0}\n</assistant>{1}";
            var nextRoleTag = nextRole.HasValue ? $"\n{GenerateTag(nextRole.Value, true)}\n" : string.Empty;

            if (message.Role == ConversationRole.User)
            {
                var userMessage = new StringBuilder();
                if (!message.ImageIndex.IsNullOrEmpty())
                {
                    foreach (var imageIndex in message.ImageIndex)
                    {
                        var imageUrl = GetMediaUrl(imageIndex.Key, imageIndex.Value);
                        if (imageUrl != null)
                            userMessage.Append($"![Image{imageIndex.Key}]({imageUrl})");
                    }
                }
                if (!message.AudioIndex.IsNullOrEmpty())
                {
                    foreach (var audioIndex in message.AudioIndex)
                    {
                        var audioUrl = GetMediaUrl(audioIndex.Key, audioIndex.Value);
                        if (audioUrl != null)
                            userMessage.Append($"![Image{audioIndex.Key}]({audioUrl})");
                    }
                }
                if (!message.VideoIndex.IsNullOrEmpty())
                {
                    foreach (var videoIndex in message.VideoIndex)
                    {
                        var videoUrl = GetMediaUrl(videoIndex.Key, videoIndex.Value);
                        if (videoUrl != null)
                            userMessage.Append($"![Video{videoIndex.Key}]({videoUrl})");
                    }
                }
                userMessage.Append(message.Content);
                return string.Format(user, userMessage, nextRoleTag).ReplaceLineEndings("\n");
            }

            var result = message.Role switch
            {
                ConversationRole.User => string.Format(user, message.Content, nextRoleTag),
                ConversationRole.System => string.Format(system, message.Content, nextRoleTag),
                ConversationRole.Assistant => string.Format(assistant, message.Content, nextRoleTag),
                _ => throw new NotImplementedException()
            };
            return result.ReplaceLineEndings("\n");
        }


        /// <summary>
        /// Gets the URL the markdown view loads a media file from.
        /// </summary>
        /// <param name="index">The media index.</param>
        /// <param name="filename">The filename.</param>
        /// <returns>The URL, or <c>null</c> if the file is not an existing local media file.</returns>
        private static string GetMediaUrl(int index, string filename)
        {
            if (index < 0)
                return null;

            // Validated before the file is touched, the path may come from a loaded conversation
            var mediaUrl = MarkdownConverter.GetResourceUrl(filename);
            if (mediaUrl == null || !File.Exists(filename))
                return null;

            return mediaUrl;
        }


        private static string GenerateTag(ConversationRole role, bool isOpen)
        {
            return role switch
            {
                ConversationRole.System => isOpen ? "<system>" : "</system>",
                ConversationRole.User => isOpen ? "<user>" : "</user>",
                ConversationRole.Assistant => isOpen ? "<assistant>" : "</assistant>",
                _ => throw new NotImplementedException()
            };
        }
    }
}
