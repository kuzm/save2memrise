if (typeof module !== 'undefined' 
        && typeof module.exports !== 'undefined') {
    // Constants for frame.js
    module.exports.save2memriseApiUrl = "http://localhost:5001/v1/";
    module.exports.closeWindowTimeout = 10000;
    module.exports.extensionId = 'bhjnibbfgdjnooiaplgfhgfibclgmpno'; // local
} else {
    // Constants for popup.js
    window.frameUrl = "frame.html";
}