var $ = jQuery = require('jquery');
window.jQuery = $; // Assure it's available globally.
var s = require('./semantic/dist/semantic.min.js');
var cookie = require('js-cookie');
var cfg = require('./config.js');

var _storedValues = {};

$(document).ready(function() {
  console.log('Frame is ready');

  console.log("Requesting init data...");
  sendMessage({ requestData: true }, 
    function(response) {
      console.log("Init data received: " + JSON.stringify(response));
      _storedValues.memriseCookies = response.memriseCookies;
      var selectedText = response.selectedText;
      if (selectedText)
      {
        storeTermText(selectedText);
        storeDefinitionText('');
      }
      
      initTermAndDefinitionInputs()
      initLanguageDropdowns();
    
      $("#saveWordBtn")
        .on('click', saveWord);
    });
});

function retrieveTermText()
{
  var val = cookie.get('termText') || '';
  console.log('Retrieving termText: ' + val);
  return val;
}

function storeTermText(val)
{
  console.log('Storing termText: ' + val);
  cookie.set('termText', val);
}

function retrieveTermLang()
{
  return cookie.get('termLanguage');
}

function retrieveDefinitionText()
{
  var val = cookie.get('definitionText') || '';
  console.log('Retrieving definitionText: ' + val);
  return val;
}

function storeDefinitionText(val)
{
  console.log('Storing definitionText: ' + val);
  cookie.set('definitionText', val);
}

function retrieveDefinitionLang()
{
  return cookie.get('definitionLanguage');
}

function initTermAndDefinitionInputs() {
  setWordTxtValue(retrieveTermText());
  setDefinitionTxtValue(retrieveDefinitionText());

  $("#wordTxt").on('input', onWordTxtInput);
  $("#definitionTxt").on('change', onDefinitionTextChange);
}

function onWordTxtInput()
{
  var val = getWordTxtValue();
  console.log("on word txt input: val=" + val);
  storeTermText(val);
}

function onDefinitionTextChange() {
  var definitionText = getDefinitionTxtValue();

  console.log("Storing definition '" + definitionText + "'");
  storeDefinitionText(definitionText);
}

function dropdownTermLanguages(selectedValue) {
  if (!selectedValue) {
    selectedValue = '6';
  }

  var langs = [
    {
      name: 'English',
      value: '6'
    },
    {
      name: 'German',
      value: '4'
    },
    {
      name: 'Russian',
      value: '10'
    }
  ];

  for (var i = 0; i < langs.length; i++) {
    var lang = langs[i];
    if (lang.value == selectedValue) {
      lang.selected = true;
      break;
    }
  }
  
  return langs.slice(0); // clone
}

function dropdownDefinitionLanguages(selectedValue) {
  if (!selectedValue) {
    selectedValue = '6';
  }

  var langs = [
    {
      name: 'English',
      value: '6'
    },
    {
      name: 'German',
      value: '879'
    },
    {
      name: 'Russian',
      value: '10'
    }
  ];

  for (var i = 0; i < langs.length; i++) {
    var lang = langs[i];
    if (lang.value == selectedValue) {
      lang.selected = true;
      break;
    }
  }
  
  return langs.slice(0); // clone
}

function initLanguageDropdowns() {
  $('#wordLang')
    .dropdown({
      values: dropdownTermLanguages(retrieveTermLang()),
      onChange: storeTermLanguage
    });

  $('#definitionLang')
    .dropdown({
      values: dropdownDefinitionLanguages(retrieveDefinitionLang()),
      onChange: storeDefinitionLanguage
    });
}

function storeTermLanguage(termLang) {
  cookie.set('termLanguage', termLang);
}

function storeDefinitionLanguage(definitionLang) {
  cookie.set('definitionLanguage', definitionLang);
}

function getWordTxtValue()
{
  var value = $("#wordTxt").val();
  console.log("get word txt text: " + value);
  return value;
}

function setWordTxtValue(val)
{
  console.log("set word txt val: " + val);
  $("#wordTxt").val(val);
}

function getWordLangValue()
{
  return $("#wordLang").dropdown("get value");
}

function getDefinitionTxtValue()
{
  return $("#definitionTxt").val();
}

function setDefinitionTxtValue(val)
{
  $("#definitionTxt").val(val);
}

function getDefinitionLangValue()
{
  return $("#definitionLang").dropdown("get value");
}

function saveWord() {
  console.log('Saving requested');

  var body = {
    term: getWordTxtValue(),
    termLang: getWordLangValue(),
    def: getDefinitionTxtValue(),
    defLang: getDefinitionLangValue()
  }

  console.log("Current input data: " + JSON.stringify(body));

  if (!body.term || !body.def) {
    //TODO 
    console.error("Both word and definition must be provided");
    return;
  }

  if (!body.termLang || !body.defLang) {
    //TODO 
    console.error("Both word language and definition language must be provided");
    return;
  }

  $("#savingMessageDimmer")
    .dimmer('show');

  console.log("Requesting memrise cookies...");
  sendMessage({ requestData: true }, 
    function(response) {
      console.log("Memrise cookies received: " + JSON.stringify(response));
      var memriseCookies = response.memriseCookies;
      sendWordSavingRequest(body, memriseCookies);
    });
};

function sendWordSavingRequest(body, memriseCookies) {
  var url = cfg.save2memriseApiUrl + "courses/default/items";
  var xhr = new XMLHttpRequest();
  xhr.open("PUT", url, true);
  xhr.setRequestHeader("Content-Type", "application/json");
  xhr.setRequestHeader("Memrise-Cookies", JSON.stringify(memriseCookies));
  
  xhr.onreadystatechange = function() {
    handleWordSavingResponse(xhr);
  };
  xhr.send(JSON.stringify(body));
}

function handleWordSavingResponse(xhr) {
  if (xhr.readyState == XMLHttpRequest.DONE) {
    var closeTimeout = cfg.closeWindowTimeout;
    $("#savingMessageDimmer")
      .dimmer('hide');

    if (xhr.status == 403) {
      console.warn('Saving failed: forbidden');
      $("#forbiddenMessageDimmer")
        .dimmer('show');
      closeTimeout *= 3;
    }
    else if (xhr.status == 502) {
      console.error('Saving failed: bad gateway');
      $("#errorMessageDimmer")
        .dimmer('show');
    }
    else if (xhr.status != 200) {
      console.warn('Saving failed: ' + xhr.status);
      $("#errorMessageDimmer")
        .dimmer('show');
    }
    else if (xhr.status == 200) {
      console.info('Saved with success');

      // JSON.parse does not evaluate the attacker's scripts.
      var resp = JSON.parse(xhr.responseText);
      console.log("Word saving response: " + JSON.stringify(resp));
        
      $("#savedMessageDimmer")
        .dimmer('show');

      // Reset inputs 
      setWordTxtValue('');
      setDefinitionTxtValue('');
      storeTermText('');
      storeDefinitionText('');
    }

    // Hide the current window
    setTimeout(function () {
      sendMessage({ requestPopupClosing: true }, function(){} );
    }, closeTimeout/*ms*/);

  } 
}

function isFrameLocal() {
  return window.location.host == "localhost" || window.location.host == "127.0.0.1";
}

function sendMessage(message, callback)
{
  if (isFrameLocal()) {
    chrome.runtime.sendMessage(message, callback);
  } else {
    chrome.runtime.sendMessage(cfg.extensionId, message, callback);
  }
}