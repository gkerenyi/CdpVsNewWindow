using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CdpVsNewWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> log = new();
        private readonly CoreWebView2CreationProperties webView2CreationProperties;

        private bool enableLog = true;

        private int newWindowTop;
        private int newWindowLeft;

        public event PropertyChangedEventHandler? PropertyChanged;

        Dictionary<CoreWebView2DevToolsProtocolEventReceiver, WebView2> cdpEventReceiverToWebView = new();

        public MainWindow()
        {
            InitializeComponent();

            this.Top = 0;
            this.Left = 1100;

            this.webView2CreationProperties = new CoreWebView2CreationProperties();

            var executablePath = GetCanaryWebViewPathIfAvailable();
            if (executablePath != null)
            {
                this.webView2CreationProperties.BrowserExecutableFolder = executablePath;
            }

            _ = InitializeAsync();
        }

        private string? GetCanaryWebViewPathIfAvailable() =>
            Directory
                .EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge SxS\Application"), "*", SearchOption.TopDirectoryOnly)
                .Where(path => Version.TryParse(Path.GetFileName(path), out _))
                .FirstOrDefault();

        public ObservableCollection<string> Log => this.log;

        private async Task InitializeAsync()
        {
            this.WebView.CreationProperties = this.webView2CreationProperties;
            await this.WebView.EnsureCoreWebView2Async();
            await this.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync();

            this.WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            this.WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            this.WebView.NavigateToString(HTML.OpenWindow);
            LogEvent($"Using WebView2 version {this.WebView.CoreWebView2.Environment.BrowserVersionString}");
        }


        private void LogEvent(string @event)
        {
            if (this.enableLog)
            {
                this.log.Add($"[{DateTime.Now.ToString("hh:mm:ss.ffff")}] {@event}");
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Log)));
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"NavigationStarting - '{webView.Source}'");
            }
        }

        private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"ContentLoading - '{webView.Source}'");
            }
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                LogEvent($"NavigationCompleted - '{webView.Source}'");
            }
        }

        private void CoreWebView2_NewWindowRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
        {
            var _deferral = e.GetDeferral();

            _ = Dispatcher.InvokeAsync(() => OpenNewWindowAsync(e, _deferral));
        }

        private async void CoreWebView2_DevToolsRequestPaused(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            if (sender is CoreWebView2DevToolsProtocolEventReceiver receiver)
            {
                if (TryDeserializeJson(e.ParameterObjectAsJson, out RequestPausedEventArgs request))
                {
                    try
                    {
                        var webView = cdpEventReceiverToWebView[receiver];
                        if (request.Request.Url.ToString().Contains(".png") && !request.Request.Url.ToString().Contains("show"))
                        {
                            var param = $"{{\"requestId\":\"{request.RequestId}\", \"errorReason\":\"BlockedByClient\"}}";
                            if (string.IsNullOrEmpty(e.SessionId))
                            {
                                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Fetch.failRequest", param);
                            }
                            else
                            {
                                await webView.CoreWebView2.CallDevToolsProtocolMethodForSessionAsync(e.SessionId, "Fetch.failRequest", param);
                            }
                        }
                        else
                        {
                            var param = $"{{\"requestId\":\"{request.RequestId}\"}}";
                            if (string.IsNullOrEmpty(e.SessionId))
                            {
                                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Fetch.continueRequest", param);
                            }
                            else
                            {
                                await webView.CoreWebView2.CallDevToolsProtocolMethodForSessionAsync(e.SessionId, "Fetch.continueRequest", param);
                            }
                        }
                    }
                    catch
                    { }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private async void CoreWebView2_DevToolsAttachedToTarget(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            if (sender is CoreWebView2DevToolsProtocolEventReceiver receiver)
            {
                if (TryDeserializeJson(e.ParameterObjectAsJson, out AutoAttachedSession session))
                {
                    var sessionId = session.SessionId;
                    if (string.IsNullOrEmpty(e.SessionId))
                    {
                        var webView = cdpEventReceiverToWebView[receiver];
                        await webView.CoreWebView2.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Fetch.enable", "{\"patterns\":[{\"requestStage\":\"Request\"}]}");

                        // Try to resume the target. This can fail if the target was destroyed before we manage to resume it with "runIfWaitingForDebugger"
                        try
                        {
                            await webView.CoreWebView2.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Runtime.runIfWaitingForDebugger", "{}");
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public static bool TryDeserializeJson<T>(string json, out T deserializedObject)
        {
            deserializedObject = default;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                deserializedObject = JsonSerializer.Deserialize<T>(json, options);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task OpenNewWindowAsync(CoreWebView2NewWindowRequestedEventArgs e, CoreWebView2Deferral deferral)
        {
            this.newWindowLeft += 20;
            this.newWindowTop += 20;

            Window window = new Window
            {
                Width = Width,
                Height = Height,
                Left = this.newWindowLeft,
                Top = this.newWindowTop
            };

            var newWebView = new WebView2();
            newWebView.CreationProperties = this.webView2CreationProperties;

            window.Owner = this;
            window.Content = newWebView;
            window.Show();

            await newWebView.EnsureCoreWebView2Async();
            LogEvent($"Creating new WebView2 version {newWebView.CoreWebView2.Environment.BrowserVersionString}");

            newWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            newWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            newWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            newWebView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;

            var fetchReceiver = newWebView.CoreWebView2.GetDevToolsProtocolEventReceiver("Fetch.requestPaused");
            cdpEventReceiverToWebView[fetchReceiver] = newWebView;
            fetchReceiver.DevToolsProtocolEventReceived += CoreWebView2_DevToolsRequestPaused;

            var attachedToTargetReceiver = newWebView.CoreWebView2.GetDevToolsProtocolEventReceiver("Target.attachedToTarget");
            cdpEventReceiverToWebView[attachedToTargetReceiver] = newWebView;
            attachedToTargetReceiver.DevToolsProtocolEventReceived += CoreWebView2_DevToolsAttachedToTarget;

            if (this.CdpTiming.SelectedIndex == 0)
            {
                LogEvent($"Calling Fetch.enable");
                await newWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Fetch.enable", "{\"patterns\":[{\"requestStage\":\"Request\"}]}");
                LogEvent($"Called Fetch.enable");
                LogEvent($"Calling Target.setAutoAttach");
                await newWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Target.setAutoAttach", "{\"autoAttach\":true,\"waitForDebuggerOnStart\":true,\"flatten\":true}");
                LogEvent($"Called Target.setAutoAttach");
            }

            LogEvent($"Assigning NewWindow");
            e.NewWindow = newWebView.CoreWebView2;
            LogEvent($"Assigned NewWindow");

            if (this.CdpTiming.SelectedIndex == 1)
            {
                if (this.Delay.SelectedIndex > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * this.Delay.SelectedIndex));
                }
                LogEvent($"Calling Fetch.enable");
                await newWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Fetch.enable", "{\"patterns\":[{\"requestStage\":\"Request\"}]}");
                LogEvent($"Called Fetch.enable");
                LogEvent($"Calling Target.setAutoAttach");
                await newWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Target.setAutoAttach", "{\"autoAttach\":true,\"waitForDebuggerOnStart\":true,\"flatten\":true}");
                LogEvent($"Called Target.setAutoAttach");
            }

            e.Handled = true;
            deferral.Complete();
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            var builder = new StringBuilder();

            foreach (var @event in this.log)
            {
                builder.AppendLine(@event);
            }

            Clipboard.SetText(builder.ToString());
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            this.log.Clear();
        }

        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox textBox &&
                (Uri.TryCreate(textBox.Text, UriKind.Absolute, out var uri) || Uri.TryCreate($"https://{textBox.Text}", UriKind.Absolute, out uri)))
            {
                this.WebView.CoreWebView2.Navigate(uri.AbsoluteUri);
            }
        }

        private void OpenDefaultHomepage(object sender, RoutedEventArgs e)
        {
            this.WebView.NavigateToString(HTML.OpenWindow);
        }
    }
}
