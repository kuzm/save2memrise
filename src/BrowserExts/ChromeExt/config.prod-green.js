if (typeof module !== 'undefined' 
        && typeof module.exports !== 'undefined') {
    // Constants for frame.js
    module.exports.save2memriseApiUrl = "https://api.prod-green.save2memrise.com/v1/";
    module.exports.closeWindowTimeout = 10000;
    module.exports.extensionId = 'jedpoleopoehklpioonelookacalmcfk'; // web store
} else {
    // Constants for popup.js
    window.frameUrl = "https://chromeext.prod-green.save2memrise.com/frame.html";
}