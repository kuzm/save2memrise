
document.addEventListener('DOMContentLoaded', function() { 
  initListeners();
  initFrame();
}, false);

function initFrame() {
  var iframe = document.getElementById("iframe");
  iframe.setAttribute('src', window.frameUrl);
}

function initListeners() {
  if (isFrameLocal()) {
    chrome.runtime.onMessage.addListener(handleMessage);
  } else {
    chrome.runtime.onMessageExternal.addListener(handleMessage);
  }
}

function isFrameLocal() {
  return !window.frameUrl.startsWith("https://");
}

function handleMessage(request, sender, sendResponse) {
  console.log("Message was received: " + JSON.stringify(request));
  if (request.requestData) {
    console.log("Data requested");
    var selectedText = getCookie('selectedText');
    setCookie('selectedText', selectedText, -1);
    
    var memriseUrl = "https://decks.memrise.com/ajax/courses/dashboard/";
    getMemriseCookies(memriseUrl, cookies => {
        var memriseCookies = cookies.map(v => { 
            return {
                domain: v.domain,
                name: v.name,
                path: v.path,
                value: v.value
            };
        });
        console.log("Sending data...");
        sendResponse({ 
          selectedText: selectedText,
          memriseCookies: memriseCookies
        });
    });
    return true; // async
  }
  else if (request.requestPopupClosing) {
    console.log("Popup closing requested");
    window.close();
  }
}

function getMemriseCookies(url, callback) {
  chrome.cookies.getAll({url: url}, function (cookies) {
    callback(cookies);
  });
}

function getCookie(cname) {
  var name = cname + "=";
  var decodedCookie = decodeURIComponent(document.cookie);
  var ca = decodedCookie.split(';');
  for(var i = 0; i <ca.length; i++) {
      var c = ca[i];
      while (c.charAt(0) == ' ') {
          c = c.substring(1);
      }
      if (c.indexOf(name) == 0) {
          return c.substring(name.length, c.length);
      }
  }
  return "";
}

function setCookie(cname, cvalue, exdays) {
  var d = new Date();
  d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
  var expires = "expires="+d.toUTCString();
  document.cookie = cname + "=" + cvalue + ";" + expires + ";path=/";
}
