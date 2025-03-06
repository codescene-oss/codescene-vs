document.getElementById('btnSend').addEventListener('click', () => {
    const messageInput = document.getElementById('message').value;
    sendMessageToExtension({ command: 'helloFromWebview', data: messageInput });
});

function renderNames(names) {
    console.log(names);
    const list = document.getElementById('nameList');
    list.innerHTML = '';
    names.forEach(name => {
        const li = document.createElement('li');
        li.textContent = name;
        list.appendChild(li);
    });
}
