document.addEventListener('click', function(e) {
    let content = '';
    let type = 'Click'
    const codecopy = e.target.closest('.copy-code');
    const thinking = e.target.classList.contains('thinking-summary');
    if(thinking || codecopy){
        type = "Thinking"
        e.preventDefault();
    }
    if (codecopy) {
        type = "Clipboard"
        content = codecopy.closest('.copy-block')?.querySelector('.copy-content')?.textContent ?? '';
        codecopy.textContent = '✓';
        setTimeout(() => {
            codecopy.textContent = '📋';
        }, 1200);
    }
    window.chrome.webview.postMessage({
        Type: type,
        X: e.clientX,
        Y: e.clientY,
        Clipboard: content
    });
}, true);

document.addEventListener('wheel', function (e) {
    window.chrome.webview.postMessage({
        Type: "Wheel",
        Y: e.deltaY,
    });
}, true);

document.addEventListener('copy', function(e) {
    let content = '';
    const selection = window.getSelection();
    if (selection && selection.rangeCount > 0) {
        content = selection.toString();
    }
    window.chrome.webview.postMessage({
        Type: 'Clipboard',
        Clipboard: content
    });
}, true);

const sendSize = () => {
    chrome.webview.postMessage({
        Type: 'Resize',
        X: document.body.scrollWidth,
        Y: document.body.scrollHeight
    });
};

const observer = new ResizeObserver(sendSize);
observer.observe(document.documentElement);
sendSize();

function getStreamMarker() {
    let marker = document.getElementById("stream-marker");
    if (!marker) {
        marker = document.createElement("div");
        marker.id = "stream-marker";
        document.body.appendChild(marker);
    }
    return marker;
}

function updateStreamContent(html) {
    const marker = getStreamMarker();
    while (marker.nextSibling) {
        marker.nextSibling.remove();
    }
    marker.insertAdjacentHTML("afterend", html);
}

function commitStreamContent() {
    const marker = getStreamMarker();
    document.body.appendChild(marker);
}

function clearStreamContent() {
    const marker = getStreamMarker();
    while (marker.nextSibling) {
        marker.nextSibling.remove();
    }
}

function clearBody() {
    document.body.innerHTML = '<div id="stream-marker"></div>';
}

function toggleThinking(visible) {
    document.querySelectorAll(".thinking-panel").forEach(panel => {
        if (visible) {
            panel.setAttribute("open", "");
        } else {
            panel.removeAttribute("open");
        }
    });
}