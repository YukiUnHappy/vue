// ==UserScript==
// @name         OtogiFrontierHook
// @version      1.2
// @description  Mosaic No, Uncensored Yes.
// @icon         https://www.dmm.co.jp/favicon.ico
// @author       Yuki
// @match        http://otogi-api.trafficmanager.net/Content/Atom*
// @run-at       document-start
// @grant        none
// @require      https://cdnjs.cloudflare.com/ajax/libs/xhook/1.4.9/xhook.min.js
// ==/UserScript==

xhook.before(function (request, callback) {
    //http://otogi.azureedge.net/Release288/webGL.jsgz
    if (request.url.indexOf('azureedge') != -1 && request.url.indexOf('webGL.jsgz') != -1) {
        var jsgz = 'https://otogi.dmmowari.ga/' + request.url.substring(request.url.indexOf('Release'));
        fetch(jsgz, {method: 'HEAD'}).then((r) => {
            if (r.status == 200)
                request.url = jsgz;
            callback();
            xhook.disable();
            console.log("[OF] Hook is hitted");
        });
    }
    else callback();
});

console.log("[OF] Hook Installed");