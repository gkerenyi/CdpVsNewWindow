# ScriptVsNewWindow

Test cases for calling Chrome DevTools Protocol methods `Fetch.enable` and `Target.setAutoAttach` when opening a new window and navigating to a URL.

## Settings
- Call CDP methods: sets whether the Chrome DevTools Protocol methods to enable request processing is called before or after calling `CoreWebView2NewWindowRequestedEventArgs.NewWindow`
- Delay (ms): parameter of an `await Task.Delay()` call before calling the CDP methods, which is used to simulate calling other async methods before calling CDP methods

# Expected Behavior
The project loads an embedded web page with a link. When the link is clicked a new window should appear containing a WebView2 instance that has:
  - 10 images in an iframe. The 5th and 10th image is displayed and contains the text "Must not be blocked", the other images are not loaded (blocked).
  - 18 images at the bottom of the page. The first and last image is displayed and contains the text "Must not be blocked", the other images are not loaded (blocked).

# Actual Behavior
- If "Call CDP methods" is set to Before:
   + All images are shown, including the ones that should have been blocked. It seems that calling the CDP methods has no effect before setting `CoreWebView2NewWindowRequestedEventArgs.NewWindow`
- If "Call CDP methods" is set to After:
   + Depending on the Delay setting, some or all of the images that should have been blocked are shown instead. There seems to be a race condition, because navigation is started after `CoreWebView2NewWindowRequestedEventArgs.NewWindow` is set, possibly when the thread is relinquished to WebView2 during the subsequent async call. As a result of navigation starting before `Fetch` is enabled, some web requests do not reach `CoreWebView2_DevToolsRequestPaused`, because `Fetch` does not have a chance to get enabled before that.

# Requirements
This project requires:
  1. .NET6 SDK
  2. WebView2 canary version. To test you need to install [Edge Canary](https://www.microsoftedgeinsider.com/en-us/download/canary).
