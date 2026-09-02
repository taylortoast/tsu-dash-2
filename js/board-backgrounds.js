(function () {
  "use strict";

  const logoPath = "source_docs/images/logo.png";
  const backgrounds = [
    "source_docs/images/bg.jpg",
    "source_docs/images/bg2.jpg",
    "source_docs/images/bg3.jpg",
    "source_docs/images/bg4.jpg",
    "source_docs/images/bg5.jpg",
    "source_docs/images/bg6.jpg",
    "source_docs/images/bg7.jpg",
    "source_docs/images/bg8.jpg",
    "source_docs/images/bg9.jpg",
    "source_docs/images/bg10.jpg",
    "source_docs/images/bg11.jpg",
    "source_docs/images/bg12.jpg",
    "source_docs/images/bg13.jpg",
    "source_docs/images/bg14.jpg",
    "source_docs/images/bg15.jpg"
  ];

  document.querySelectorAll("[data-logo]").forEach((logo) => {
    logo.src = logoPath;
  });

  const layers = Array.from(document.querySelectorAll("[data-bg-layer]"));
  if (!layers.length || !backgrounds.length) return;

  let imageIndex = 0;
  let activeLayer = 0;

  layers[activeLayer].style.backgroundImage = `url("${backgrounds[imageIndex]}")`;
  layers[activeLayer].classList.add("is-visible");

  window.setInterval(() => {
    imageIndex = (imageIndex + 1) % backgrounds.length;
    const nextLayer = activeLayer === 0 ? 1 : 0;
    layers[nextLayer].style.backgroundImage = `url("${backgrounds[imageIndex]}")`;
    layers[nextLayer].classList.add("is-visible");
    layers[activeLayer].classList.remove("is-visible");
    activeLayer = nextLayer;
  }, 14000);
}());
