function sendMessageToExtension(message) {
    window.chrome.webview.postMessage(message);
}

window.chrome.webview.addEventListener('message', event => {
    const message = event.data;
    renderNames(message.Data);
});
