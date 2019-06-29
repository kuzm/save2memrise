var $ = jQuery = require('jquery');
window.jQuery = $; // Assure it's available globally.
var s = require('./semantic/dist/semantic.min.js');
var cookie = require('js-cookie');
var cfg = require('./config.js');
var version = require('./version.json');

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
    
      $("#saveWordBtn")
        .on('click', saveWord);

      getCourses();
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

function retrieveCourse()
{
  return cookie.get('course');
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

function dropdownCourse(selectedValue, courses) {
  let courseItems = [];

  for (var i = 0; i < courses.length; i++) {
    let course = courses[i];
    let courseItem = {
      name: course.name,
      value: `${course.id}/${course.slug}`
    };

    if (courseItem.value == selectedValue) {
      courseItem.selected = true;
    }

    courseItems.push(courseItem);
  }
  
  return courseItems;
}

function initCourseDropdown(coursesResponse) {
  $('#course')
    .dropdown({
      values: dropdownCourse(retrieveCourse(), coursesResponse.courses),
      onChange: storeCourse,
      placeholder: 'Select a course'
    });
}

function storeCourse(course) {
  cookie.set('course', course);
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

function getCourseValue()
{
  return $("#course").dropdown("get value");
}

function getDefinitionTxtValue()
{
  return $("#definitionTxt").val();
}

function setDefinitionTxtValue(val)
{
  $("#definitionTxt").val(val);
}

function getCourses() {
  console.log('Courses requested');

  $("#loadingCoursesDimmer")
    .dimmer('show');

  console.log("Requesting memrise cookies...");
  sendMessage({ requestData: true }, 
    function(response) {
      console.log("Memrise cookies received: " + JSON.stringify(response));
      var memriseCookies = response.memriseCookies;
      sendCoursesRequest(memriseCookies);
    });
};

function sendCoursesRequest(memriseCookies) {
  var url = cfg.save2memriseApiUrl + "courses";
  var xhr = new XMLHttpRequest();
  xhr.open("GET", url, true);
  xhr.setRequestHeader("Content-Type", "application/json");
  xhr.setRequestHeader("Memrise-Cookies", JSON.stringify(memriseCookies));
  
  xhr.onreadystatechange = function() {
    handleCoursesResponse(xhr);
  };
  xhr.send();
}

function handleCoursesResponse(xhr) {
  if (xhr.readyState == XMLHttpRequest.DONE) {
    $("#loadingCoursesDimmer")
      .dimmer('hide');

    if (xhr.status == 403) {
      console.warn('Courses retrieval failed: forbidden');
      $("#forbiddenMessageDimmer")
        .dimmer('show');
    }
    else if (xhr.status == 502) {
      console.error('Courses retrieval failed: bad gateway');
      $("#errorMessageDimmer")
        .dimmer('show');
    }
    else if (xhr.status != 200) {
      console.warn('Courses retrieval failed: ' + xhr.status);
      $("#errorMessageDimmer")
        .dimmer('show');
    }
    else if (xhr.status == 200) {
      console.info('Courses retrieved with success');

      // JSON.parse does not evaluate the attacker's scripts.
      var resp = JSON.parse(xhr.responseText);
      console.log("Courses retrieval response: " + JSON.stringify(resp));
      
      // Reset inputs 
      initCourseDropdown(resp);
    }
  } 
}

function saveWord() {
  console.log('Saving requested');

  let course = getCourseValue();

  let body = {
    term: getWordTxtValue(),
    def: getDefinitionTxtValue()
  }

  console.log(`Current input data: term=${body.term}, def=${body.def}, course=${course}`);

  if (!body.term || !body.def) {
    //TODO 
    console.error("Both word and definition must be provided");
    return;
  }

  if (!course) {
    //TODO 
    console.error("Course must be provided");
    return;
  }

  $("#savingMessageDimmer")
    .dimmer('show');

  console.log("Requesting memrise cookies...");
  sendMessage({ requestData: true }, 
    function(response) {
      console.log("Memrise cookies received: " + JSON.stringify(response));
      var memriseCookies = response.memriseCookies;
      sendWordSavingRequest(course, body, memriseCookies);
    });
};

function sendWordSavingRequest(course, body, memriseCookies) {
  var url = cfg.save2memriseApiUrl + `courses/${course}/items`;
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