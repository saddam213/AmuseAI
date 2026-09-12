using Amuse.App.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    public partial class MarkdownElement : BaseControl
    {
        private static readonly SemaphoreSlim _environmentLock = new(1, 1);
        private static CoreWebView2Environment _environment;
        private readonly SemaphoreSlim _createLock = new(1, 1);
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        private readonly SemaphoreSlim _switchLock = new(1, 1);
        private WebView2 WebViewer;
        private bool _isInitialized;
        private bool _isUpdatePending;
        private bool _isContentReady;
        private double _webViewerDpiX = 1;
        private double _webViewerDpiY = 1;
        private int _cacheLength;
        private static readonly int[] _invalidContextMenuItems =
        [
            33000, // Back
            33001, // Forward
            33002, // Reload
            35003, // Print
            35004, // SaveAs
            35016  // SendTo
        ];

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownElement"/> class.
        /// </summary>
        public MarkdownElement()
        {
            SaveCommand = new AsyncRelayCommand<bool>(SaveAsync, (s) => IsInputEnabled);
            CopyCommand = new AsyncRelayCommand<bool>(CopyAsync, (s) => IsInputEnabled);
            CopyResponseCommand = new AsyncRelayCommand<bool>(CopyResponseAsync, (s) => IsInputEnabled);
            CopyThinkingCommand = new AsyncRelayCommand<bool>(CopyThinkingAsync, (s) => IsInputEnabled);
            InitializeComponent();
            TextViewer.TextChanged += (s, e) =>
            {
                NotifyPropertyChanged(nameof(Length));
                IsContentReady = Length > 0;
            };
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(MarkdownElement));
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MarkdownElement), new PropertyMetadata<MarkdownElement, string>((c, e) => c.SetTextAsync(e)));
        public static readonly DependencyProperty IsMarkdownEnabledProperty = DependencyProperty.Register(nameof(IsMarkdownEnabled), typeof(bool), typeof(MarkdownElement), new PropertyMetadata<MarkdownElement>((c) => c.SwitchViewAsync()));
        public static readonly DependencyProperty IsScrollToBottomEnabledProperty = DependencyProperty.Register(nameof(IsScrollToBottomEnabled), typeof(bool), typeof(MarkdownElement));
        public static readonly DependencyProperty IsThinkingVisibleProperty = DependencyProperty.Register(nameof(IsThinkingVisible), typeof(bool), typeof(MarkdownElement), new PropertyMetadata<MarkdownElement>((c) => c.UpdateThinking()) { DefaultValue = true });
        public static readonly DependencyProperty IsInputEnabledProperty = DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool), typeof(MarkdownElement), new PropertyMetadata(true));
        public static readonly DependencyProperty IsContextMenuEnabledProperty = DependencyProperty.Register(nameof(IsContextMenuEnabled), typeof(bool), typeof(MarkdownElement), new PropertyMetadata(true));
        public AsyncRelayCommand<bool> SaveCommand { get; }
        public AsyncRelayCommand<bool> CopyCommand { get; }
        public AsyncRelayCommand<bool> CopyResponseCommand { get; }
        public AsyncRelayCommand<bool> CopyThinkingCommand { get; }
        public bool IsConversationMode { get; set; }
        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the markdown.
        /// </summary>
        public string Markdown
        {
            get { return (string)GetValue(MarkdownProperty); }
            set { SetValue(MarkdownProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the markdown view is enabled.
        /// </summary>
        public bool IsMarkdownEnabled
        {
            get { return (bool)GetValue(IsMarkdownEnabledProperty); }
            set { SetValue(IsMarkdownEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is scroll to bottom enabled.
        /// </summary>
        public bool IsScrollToBottomEnabled
        {
            get { return (bool)GetValue(IsScrollToBottomEnabledProperty); }
            set { SetValue(IsScrollToBottomEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets the thinking visibility.
        /// </summary>
        public bool IsThinkingVisible
        {
            get { return (bool)GetValue(IsThinkingVisibleProperty); }
            set { SetValue(IsThinkingVisibleProperty, value); }
        }

        /// <summary>
        /// Gets or sets if input actiona are enabled.
        /// </summary>
        public bool IsInputEnabled
        {
            get { return (bool)GetValue(IsInputEnabledProperty); }
            set { SetValue(IsInputEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets if ContextMenus are enabled.
        /// </summary>
        public bool IsContextMenuEnabled
        {
            get { return (bool)GetValue(IsContextMenuEnabledProperty); }
            set { SetValue(IsContextMenuEnabledProperty, value); }
        }

        /// <summary>
        /// Gets the markup length.
        /// </summary>
        public int Length => TextViewer?.Text?.Length ?? 0;

        /// <summary>
        /// Gets a value indicating whether this instance is content ready.
        /// </summary>
        public bool IsContentReady
        {
            get { return _isContentReady; }
            private set { SetProperty(ref _isContentReady, value); }
        }


        /// <summary>
        /// Set the body content.
        /// </summary>
        /// <param name="content">The content.</param>
        public async Task SetTextAsync(string content)
        {
            await ClearAsync();
            TextViewer.AppendText(content);
            await UpdateWebView(true);
            await CommitHtmlBody();
            _cacheLength = Length;
            if (IsScrollToBottomEnabled)
                ScrollViewerHost.ScrollToBottom();
        }


        /// <summary>
        /// Appends text content to the end of the body.
        /// </summary>
        /// <param name="content">The content.</param>
        public Task AppendStreamAsync(string content)
        {
            TextViewer.AppendText(content);
            _ = UpdateWebView();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Add a complete message and cache the body content.
        /// </summary>
        /// <param name="content">The content.</param>
        public async Task AppendTextAsync(string content)
        {
            TextViewer.AppendText(content);
            await UpdateWebView(true);
            await CommitHtmlBody();
            _cacheLength = Length;
        }


        /// <summary>
        /// Reload the body content
        /// </summary>
        public async Task ReloadAsync()
        {
            _cacheLength = 0;
            await ClearHtmlBody();
            await UpdateWebView(true);
        }


        /// <summary>
        /// Clears body content.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ClearAsync()
        {
            _cacheLength = 0;
            TextViewer.Clear();
            await ClearHtmlBody();
        }


        /// <summary>
        /// Clears body content.
        /// </summary>
        /// <returns>Task.</returns>
        public Task CloseAsync()
        {
            _cacheLength = 0;
            TextViewer.Clear();
            DestroyWebView();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Gets the plain text.
        /// </summary>
        public string GetPlainText()
        {
            return TextViewer?.Text;
        }


        /// <summary>
        /// Gets the thinking text.
        /// </summary>
        public string GetThinkingText()
        {
            return Utils.GetThinkingText(TextViewer?.Text, IsConversationMode);
        }


        /// <summary>
        /// Gets the response text.
        /// </summary>
        public string GetResponseText()
        {
            return Utils.GetResponseText(TextViewer?.Text, IsConversationMode);
        }


        /// <summary>
        /// Gets the HTML page.
        /// </summary>
        public string GetHtmlPage()
        {
            return MarkdownConverter.BuildCleanHtml(TextViewer?.Text);
        }


        /// <summary>
        /// Save the content
        /// </summary>
        /// <param name="isFormatted">if set to <c>true</c> format the output.</param>
        public async Task SaveAsync(bool isFormatted)
        {
            if (!IsInputEnabled)
                return;

            if (isFormatted)
            {
                var saveFilename = await DialogService.SaveFileAsync("Save Html", "ModelOutput", filter: "Html files (*.html)|*.html", defualtExt: "html");
                if (!string.IsNullOrEmpty(saveFilename))
                {
                    await File.WriteAllTextAsync(saveFilename, MarkdownConverter.BuildCleanHtml(TextViewer.Text));
                }
            }
            else
            {
                var saveFilename = await DialogService.SaveFileAsync("Save Text", "ModelOutput", filter: "Text Files (*.md;*.txt;*.json;)|*.md;*.txt;*.json;|Html files (*.html)|*.html|All Files|*.*", defualtExt: "md");
                if (!string.IsNullOrEmpty(saveFilename))
                {
                    var content = !Path.GetExtension(saveFilename).Equals(".html")
                        ? TextViewer.Text
                        : MarkdownConverter.BuildCleanHtml(TextViewer.Text);
                    await File.WriteAllTextAsync(saveFilename, content);
                }
            }
        }


        /// <summary>
        /// Copy the content to clipboard
        /// </summary>
        /// <param name="isFormatted">if set to <c>true</c> format the output.</param>
        public async Task CopyAsync(bool isFormatted)
        {
            if (!IsInputEnabled)
                return;

            var content = isFormatted ? GetHtmlPage() : TextViewer.Text;
            await ClipboardManager.SetTextAsync(content);
        }


        /// <summary>
        /// Copy the response to clipboard
        /// </summary>
        /// <param name="isFormatted">if set to <c>true</c> format the output.</param>
        public async Task CopyResponseAsync(bool isFormatted)
        {
            if (!IsInputEnabled)
                return;

            var content = GetResponseText();
            if (isFormatted)
            {
                content = MarkdownConverter.BuildCleanHtml(content);
            }
            await ClipboardManager.SetTextAsync(content);
        }


        /// <summary>
        /// Copy the thinking to clipboard
        /// </summary>
        /// <param name="isFormatted">if set to <c>true</c> format the output.</param>
        public async Task CopyThinkingAsync(bool isFormatted)
        {
            if (!IsInputEnabled)
                return;

            var content = GetThinkingText();
            if (isFormatted)
            {
                content = MarkdownConverter.BuildCleanHtml(content);
            }
            await ClipboardManager.SetTextAsync(content);
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
        /// Switch view (Markdown/PlainText)
        /// </summary>
        private async Task SwitchViewAsync()
        {
            try
            {
                await _switchLock.WaitAsync();
                if (IsMarkdownEnabled)
                {
                    await ReloadAsync();
                    WebViewerContainer.Visibility = Visibility.Visible;
                    TextViewer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TextViewer.Height = double.NaN;
                    TextViewer.Visibility = Visibility.Visible;
                    WebViewerContainer.Visibility = Visibility.Collapsed;
                }

            }
            finally
            {
                _switchLock.Release();
            }
        }


        /// <summary>
        /// Updates the WebView.
        /// </summary>
        private async Task UpdateWebView(bool wait = false)
        {
            if (!IsMarkdownEnabled)
                return;

            _isUpdatePending = true;
            if (!await _updateLock.WaitAsync(wait ? -1 : 0))
                return;

            try
            {
                if (!_isInitialized)
                {
                    if (Length == 0)
                        return;

                    await CreateWebViewAsync();
                }

                while (_isUpdatePending)
                {
                    _isUpdatePending = false;
                    await UpdateHtmlBody();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] UpdateWebView: {ex.Message}");
            }
            finally
            {
                _updateLock.Release();
            }
        }


        /// <summary>
        /// Updates the HTML body.
        /// </summary>
        private async Task UpdateHtmlBody()
        {
            if (!_isInitialized)
                return;

            try
            {
                var markdown = TextViewer.Text[_cacheLength..];
                if (markdown.Length == 0)
                    return;

                var bodyHtml = MarkdownConverter.BuildBody(markdown, IsThinkingVisible);
                await WebViewer.CoreWebView2.ExecuteScriptAsync($"updateStreamContent({JsonSerializer.Serialize(bodyHtml)});");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] UpdateMainContent: {ex.Message}");
            }
        }


        /// <summary>
        /// Clears the HTML body.
        /// </summary>
        private async Task ClearHtmlBody()
        {
            if (!_isInitialized)
                return;

            try
            {
                await WebViewer.CoreWebView2.ExecuteScriptAsync("clearBody();");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] ClearHtmlBody: {ex.Message}");
            }
        }


        /// <summary>
        /// Commits the HTML body.
        /// </summary>
        private async Task CommitHtmlBody()
        {
            if (!_isInitialized)
                return;

            try
            {
                await WebViewer.CoreWebView2.ExecuteScriptAsync("commitStreamContent();");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] CommitHtmlBody: {ex.Message}");
            }
        }


        /// <summary>
        /// Updates the thinking section visibility.
        /// </summary>
        private async Task UpdateThinking()
        {
            if (!_isInitialized || Length == 0)
                return;

            try
            {
                await WebViewer.CoreWebView2.ExecuteScriptAsync($"toggleThinking({JsonSerializer.Serialize(IsThinkingVisible)});");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] UpdateThinking: {ex.Message}");
            }
        }


        /// <summary>
        /// Render the HTML in the WebView
        /// </summary>
        /// <param name="html">The HTML.</param>
        private async Task CreateHtmlPageAsync()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object sender, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult();
            WebViewer.NavigationCompleted += Handler;

            try
            {
                var htmlContent = MarkdownConverter.BuildEmptyHtml(FontSize, FontFamily);
                WebViewer.NavigateToString(htmlContent);
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] RenderHtml: {ex.Message}");
            }
            finally
            {
                WebViewer.NavigationCompleted -= Handler;
            }
        }


        /// <summary>
        /// Create WebView
        /// </summary>
        private async Task CreateWebViewAsync()
        {
            try
            {
                await _createLock.WaitAsync();
                if (_isInitialized)
                    return;

                CreateWebView();
                var environment = await GetEnvironmentAsync();
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.AllowHostInputProcessing = true;
                await WebViewer.EnsureCoreWebView2Async(environment, options);
                WebViewer.AllowDrop = false;
                WebViewer.AllowExternalDrop = false;
                WebViewer.Height = (int)ScrollViewerHost.ActualHeight;
                WebViewer.CoreWebView2.AddWebResourceRequestedFilter("https://resource.amuse/*", CoreWebView2WebResourceContext.All);
                WebViewer.CoreWebView2.Settings.IsScriptEnabled = true;
                WebViewer.CoreWebView2.Settings.IsWebMessageEnabled = true;
                WebViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebViewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
                WebViewer.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                WebViewer.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                WebViewer.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                WebViewer.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;
                WebViewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebViewer.CoreWebView2.Settings.IsZoomControlEnabled = false;
                WebViewer.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
                WebViewer.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                WebViewer.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = false;
                WebViewer.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                WebViewer.CoreWebView2.Settings.IsPinchZoomEnabled = false;
                WebViewer.CoreWebView2.Settings.IsReputationCheckingRequired = false;
                WebViewer.CoreWebView2.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save | CoreWebView2PdfToolbarItems.SaveAs;
                WebViewer.CoreWebView2.Settings.UserAgent = $"Amuse/{App.AppVersionDisplay}";
                var source = PresentationSource.FromVisual(WebViewer);
                if (source?.CompositionTarget != null)
                {
                    _webViewerDpiX = source.CompositionTarget.TransformToDevice.M11;
                    _webViewerDpiY = source.CompositionTarget.TransformToDevice.M22;
                }
                await CreateHtmlPageAsync();
                WebViewer.NavigationStarting += WebView_NavigationStarting;
                WebViewer.CoreWebView2.ContextMenuRequested += WebView_ContextMenuRequested;
                WebViewer.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                WebViewer.CoreWebView2.WebResourceRequested += WebView_WebResourceRequested;
                WebViewer.CoreWebView2.DownloadStarting += WebView_DownloadStarting;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] CreateWebViewAsync: {ex.Message}");
            }
            finally
            {
                _createLock.Release();
            }
        }


        /// <summary>
        /// Creates the WebView2.
        /// </summary>
        private void CreateWebView()
        {
            WebViewer = new WebView2
            {
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                DefaultBackgroundColor = Color.FromArgb(0xFF, 0x2D, 0x2D, 0x2D)
            };

            WebViewer.SetBinding(WebView2.ZoomFactorProperty, new Binding("Settings.UIScale"));
            WebViewerContainer.Children.Add(WebViewer);
        }


        /// <summary>
        /// Destroys the web view.
        /// </summary>
        private void DestroyWebView()
        {
            if (WebViewer == null)
                return;

            if (WebViewer.CoreWebView2 != null)
            {
                WebViewer.NavigationStarting -= WebView_NavigationStarting;
                WebViewer.CoreWebView2.ContextMenuRequested -= WebView_ContextMenuRequested;
                WebViewer.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
                WebViewer.CoreWebView2.WebResourceRequested -= WebView_WebResourceRequested;
                WebViewer.CoreWebView2.DownloadStarting -= WebView_DownloadStarting;
                WebViewer.CoreWebView2.Stop();
                _isInitialized = false;
            }
            WebViewerContainer.Children.Remove(WebViewer);
            WebViewer.Dispose();
            WebViewer = null;
        }


        /// <summary>
        /// Get WebView environment
        /// </summary>
        private static async Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environment != null)
                return _environment;

            await _environmentLock.WaitAsync();
            try
            {
                if (_environment != null)
                    return _environment;

                var options = new CoreWebView2EnvironmentOptions
                {
                    EnableTrackingPrevention = true,
                    AreBrowserExtensionsEnabled = false,
                    IsCustomCrashReportingEnabled = true,
                    ExclusiveUserDataFolderAccess = true,
                    AllowSingleSignOnUsingOSPrimaryAccount = false,
                    AdditionalBrowserArguments = "--disable-gpu"
                };
                _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(App.DirectoryData, "Temp"), options: options);
                return _environment;
            }
            finally
            {
                _environmentLock.Release();
            }
        }


        /// <summary>
        /// Handles the DownloadStarting event of the WebView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CoreWebView2DownloadStartingEventArgs"/> instance containing the event data.</param>
        private void WebView_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            e.Cancel = true;
        }


        /// <summary>
        /// Handles the NavigationStarting event of the WebView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        private async void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.IsUserInitiated)
                return;

            if (string.Equals(e.Uri, "about:blank", StringComparison.OrdinalIgnoreCase))
                return;

            Debug.WriteLine($"[WebView] [NavigationStarting] Uri: {e.Uri}");
            if (e.NavigationKind == CoreWebView2NavigationKind.NewDocument)
            {
                e.Cancel = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri uriResult))
                {
                    if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                    {
                        if (!Settings.IsExternalLinksAcknowledged)
                        {
                            var dialogResult = await DialogService.ShowMessageAsync("External Link", "This link was provided in a generated response and will open in your default web browser.\nGenerated content may contain incorrect/untrusted links.\n\nDo you want to continue?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Warning, true);
                            Settings.IsExternalLinksAcknowledged = dialogResult.DontAskAgain;
                            if (!dialogResult)
                                return;

                            Settings.IsExternalLinksEnabled = true;
                        }

                        if (Settings.IsExternalLinksEnabled)
                            URL.NavigateToUrl(e.Uri);
                    }
                }
            }
        }


        /// <summary>
        /// Handles the WebResourceRequested event of the WebView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CoreWebView2WebResourceRequestedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        private void WebView_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (e.ResourceContext != CoreWebView2WebResourceContext.Image && e.ResourceContext != CoreWebView2WebResourceContext.Media)
                return;

            var deferral = e.GetDeferral();
            try
            {
                var uri = new Uri(e.Request.Uri);
                var filename = Uri.UnescapeDataString(uri.LocalPath?.Trim('/') ?? "");
                if (!File.Exists(filename))
                    throw new FileNotFoundException(filename);

                var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                var disposableStream = new AutoDisposeStream(fileStream);

                e.Response = WebViewer.CoreWebView2.Environment.CreateWebResourceResponse(disposableStream, 200, "OK", $"Content-Type: {GetContentType(filename)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView] [Exception] WebResourceRequested: {ex.Message}");
                e.Response = WebViewer.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Content-Type: text/plain");
            }
            finally
            {
                deferral.Complete();
            }
        }


        /// <summary>
        /// Handles the WebMessageReceived event of the WebView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CoreWebView2WebMessageReceivedEventArgs"/> instance containing the event data.</param>
        private async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var webMessage = JsonSerializer.Deserialize<WebMessage>(e.WebMessageAsJson, Json.DefaultOptions);
            if (webMessage.Type == WebMessageType.Click)
            {
            }
            else if (webMessage.Type == WebMessageType.Thinking)
            {
                IsThinkingVisible = !IsThinkingVisible;
            }
            else if (webMessage.Type == WebMessageType.Resize)
            {
                WebViewer.Height = (int)Math.Max(ScrollViewerHost.ActualHeight - 0.5, webMessage.Y);
            }
            else if (webMessage.Type == WebMessageType.Clipboard)
            {
                if (!string.IsNullOrEmpty(webMessage.Clipboard))
                {
                    await ClipboardManager.SetTextAsync(webMessage.Clipboard.Trim());
                }
            }
            else if (webMessage.Type == WebMessageType.Wheel)
            {
                ScrollViewerHost.ScrollToVerticalOffset(Math.Clamp(ScrollViewerHost.VerticalOffset + webMessage.Y, 0, ScrollViewerHost.ScrollableHeight));
            }
        }


        /// <summary>
        /// Handles the ContextMenuRequested event of the WebView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CoreWebView2ContextMenuRequestedEventArgs"/> instance containing the event data.</param>
        private void WebView_ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (!IsContextMenuEnabled)
            {
                e.Handled = true;
                return;
            }

            var menuItems = new List<CoreWebView2ContextMenuItem>();
            for (int i = 0; i < e.MenuItems.Count; i++)
            {
                if (!_invalidContextMenuItems.Contains(e.MenuItems[i].CommandId))
                    menuItems.Add(e.MenuItems[i]);
            }

            if (e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.Page)
            {
                var menuItemCopy = CreateMenuItem("Copy                                  Ctrl+C", false, default);
                var menuItemSave = CreateMenuItem("Save As                              Ctrl+S", IsInputEnabled, SaveAsync);
                var menuItemCopyAll = CreateMenuItem("Copy Text                           Ctrl+A", IsInputEnabled, CopyAsync);
                var menuItemResponse = CreateMenuItem("Copy Response                  Ctrl+R", IsInputEnabled, CopyResponseAsync);
                var menuItemThinking = CreateMenuItem("Copy Thinking                    Ctrl+T", IsInputEnabled, CopyThinkingAsync);
                var menuItemCopyAllHtml = CreateMenuItem("Copy Html                          Ctrl+Shift+A", IsInputEnabled, CopyAsync, true);
                var menuItemResponseHtml = CreateMenuItem("Copy Response Html          Ctrl+Shift+R", IsInputEnabled, CopyResponseAsync, true);
                var menuItemThinkingHtml = CreateMenuItem("Copy Thinking Html            Ctrl+Shift+T", IsInputEnabled, CopyThinkingAsync, true);
                menuItems.Insert(0, menuItemCopy);
                menuItems.Insert(1, menuItemSave);
                menuItems.Add(menuItemCopyAll);
                menuItems.Add(menuItemResponse);
                menuItems.Add(menuItemThinking);
                menuItems.Add(WebViewer.CoreWebView2.Environment.CreateContextMenuItem(null, null, CoreWebView2ContextMenuItemKind.Separator));
                menuItems.Add(menuItemCopyAllHtml);
                menuItems.Add(menuItemResponseHtml);
                menuItems.Add(menuItemThinkingHtml);
            }

            e.MenuItems.Clear();
            foreach (var menuItem in menuItems)
                e.MenuItems.Add(menuItem);
        }


        /// <summary>
        /// Creates the menu item.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="clickFunction">The click function.</param>
        /// <param name="isEnabled">if set to <c>true</c> [is enabled].</param>
        private CoreWebView2ContextMenuItem CreateMenuItem(string label, bool isEnabled = true, Func<bool, Task> clickFunction = default, bool clickArg = false)
        {
            var menuItem = WebViewer.CoreWebView2.Environment.CreateContextMenuItem(label, null, CoreWebView2ContextMenuItemKind.Command);
            menuItem.IsEnabled = isEnabled;
            if (clickFunction != null)
            {
                menuItem.CustomItemSelected += async (s, args) => await clickFunction(clickArg);
            }
            return menuItem;
        }


        /// <summary>
        /// Handles the ScrollChanged event of the ScrollViewerHost control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ScrollChangedEventArgs"/> instance containing the event data.</param>
        private void ScrollViewerHost_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_isInitialized || WebViewer.Handle == nint.Zero || Settings == null)
                return;

            var scrollViewerRect = new Rect(0, 0, ScrollViewerHost.ActualWidth, ScrollViewerHost.ActualHeight);
            var transform = ScrollViewerHost.TransformToDescendant(WebViewer);
            if (transform == null)
                return;

            // This calculates exactly what part of the WebView is inside the ScrollViewer view port
            // and Intersect it with the actual dimension limits of the WebView2 control itself
            var visibleRect = transform.TransformBounds(scrollViewerRect);
            var webViewBounds = new Rect(0, 0, WebViewer.ActualWidth, WebViewer.ActualHeight);
            visibleRect.Intersect(webViewBounds);
            if (visibleRect.IsEmpty || visibleRect.Width <= 0 || visibleRect.Height <= 0)
            {
                _ = Native.SetWindowRgn(WebViewer.Handle, Native.CreateRectRgn(0, 0, 0, 0), true);
            }
            else
            {
                var scale = Settings.UIScale;
                var left = (int)(visibleRect.Left * _webViewerDpiX * scale);
                var top = (int)(visibleRect.Top * _webViewerDpiY * scale);
                var right = (int)(visibleRect.Right * _webViewerDpiX * scale);
                var bottom = (int)(visibleRect.Bottom * _webViewerDpiY * scale);
                _ = Native.SetWindowRgn(WebViewer.Handle, Native.CreateRectRgn(left, top, right, bottom), true);
            }
        }


        /// <summary>
        /// Gets the type of the content.
        /// </summary>
        /// <param name="path">The path.</param>
        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                // Images
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",

                // Audio
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".aac" => "audio/aac",
                ".m4a" => "audio/mp4",

                // Video
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" or ".ogv" => "video/ogg",
                ".mov" => "video/quicktime",

                // Web Core Essentials
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" or ".mjs" => "text/javascript",
                ".json" => "application/json",
                ".txt" => "text/plain",

                // Fonts
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",

                // Fallback
                _ => "application/octet-stream"
            };
        }

        private record WebMessage(WebMessageType Type, int X, int Y, string Clipboard);
        public enum WebMessageType
        {
            Click = 0,
            Resize = 1,
            Clipboard = 2,
            Thinking = 3,
            Wheel = 4
        }
    }
}