// Branche les boutons d'achat et de téléchargement sur la configuration (js/config.js).
(function () {
  "use strict";

  var config = window.CLEAN_SITE || {};
  var targets = { buy: config.checkoutUrl, download: config.downloadUrl };

  function isSafeUrl(value) {
    try {
      var url = new URL(value, window.location.href);
      return url.protocol === "https:" || url.protocol === "ms-windows-store:";
    } catch (error) {
      return false;
    }
  }

  document.querySelectorAll("[data-cta]").forEach(function (link) {
    var target = targets[link.getAttribute("data-cta")];
    if (target && isSafeUrl(target)) {
      link.setAttribute("href", target);
      return;
    }

    link.setAttribute("aria-disabled", "true");
    link.textContent = "Bientôt disponible";
    link.addEventListener("click", function (event) {
      event.preventDefault();
    });
  });
})();
