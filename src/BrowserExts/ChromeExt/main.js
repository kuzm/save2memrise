
function onClick(info, tab) {
  var selectedText = info.selectionText;
  console.log("Selected text: " + selectedText);
  //TODO check max length

  setCookie('selectedText', selectedText, 1);

  showPopup();
}

function setCookie(cookieName, cookieValue, expireDays) {
  var d = new Date();
  d.setTime(d.getTime() + (expireDays * 24 * 60 * 60 * 1000));
  var expires = "expires="+d.toUTCString();
  document.cookie = cookieName + "=" + cookieValue + ";" + expires + ";path=/";
}

function showPopup() {
  var popupWidth = 450;
  var popupHeight = 370;
  chrome.windows.create({
      url: chrome.extension.getURL("popup_without_size.html"),
      type: "popup",
      top: (screen.height - popupHeight) / 2,
      left: (screen.width - popupWidth) / 2,
      width: popupWidth,
      height: popupHeight,
      focused: true
    });
}

var context = "selection";
var title = "Save to Memrise";
var id = chrome.contextMenus
  .create({
    "title": title, 
    "contexts":[context],
    "onclick": onClick
  });
