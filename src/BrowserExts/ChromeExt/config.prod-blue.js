if (typeof module !== 'undefined' 
        && typeof module.exports !== 'undefined') {
    // Constants for frame.js
    module.exports.save2memriseApiUrl = "https://api.prod-blue.save2memrise.com/v1/";
    module.exports.closeWindowTimeout = 10000;
    module.exports.extensionId = 'jedpoleopoehklpioonelookacalmcfk'; // web store
} else {
    // Constants for popup.js
    window.frameUrl = "https://chromeext.prod-blue.save2memrise.com/frame.html";
}