# CdpVsNewWindow

Test cases for adding web resource request handlers by the following methods when opening a new window and navigating to a URL.
- Assigning a handler to `WebResourceRequested`.
- Assigning a handler to Chrome DevTools Protocol events `Fetch.requestPaused` and `Target.autoAttached`, then calling Chrome DevTools Protocol methods `Fetch.enable` and `Target.setAutoAttach` to enable event hanling.

## Settings
- Handler type: Use either `WebResourceRequested` or Chrome DevTools Protocol `Fetch` event handlers.
- Add event handlers: sets whether request handlers are assigned before or after calling `CoreWebView2NewWindowRequestedEventArgs.NewWindow`
- Delay (ms): parameter of an `await Task.Delay()` call before assigning the handler, which is used to simulate calling other methods between assigning `NewWindow` and setting up the handler.

# Expected Behavior
The project loads an embedded web page with a link. When the link is clicked a new window should appear containing a WebView2 instance that has:
  - *[Ignore when using `WebResourceRequested]* 10 images in an iframe. The 5th and 10th image is displayed and contains the text "Must not be blocked", the other images are not loaded (blocked).
  - 18 images at the bottom of the page. The first and last image is displayed and contains the text "Must not be blocked", the other images are not loaded (blocked).

# Actual Behavior
- If `Add event handlers` is set to Before:
   + All images are shown, including the ones that should have been blocked. It seems that setting up the event handlers has no effect before setting `CoreWebView2NewWindowRequestedEventArgs.NewWindow`
- If `Add event handlers` is set to After:
   + Depending on the Delay setting, some or all of the images that should have been blocked are shown instead. There seems to be a race condition, because navigation is started right after `CoreWebView2NewWindowRequestedEventArgs.NewWindow` is set. As a result of navigation starting before assigning the event handlers, some requests do not have a chance to reach the event handlers and survive without blocking.

# Requirements
This project requires:
  1. .NET6 SDK
  2. WebView2 canary version. To test you need to install [Edge Canary](https://www.microsoftedgeinsider.com/en-us/download/canary).
