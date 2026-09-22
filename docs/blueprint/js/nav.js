(function () {
  "use strict";

  var body = document.body;
  var toggle = document.getElementById("menu-toggle");
  var overlay = document.getElementById("sidebar-overlay");
  var search = document.getElementById("doc-search");

  function closeNav() {
    body.classList.remove("nav-open");
  }

  if (toggle) {
    toggle.addEventListener("click", function () {
      body.classList.toggle("nav-open");
    });
  }

  if (overlay) {
    overlay.addEventListener("click", closeNav);
  }

  document.querySelectorAll(".sidebar a").forEach(function (a) {
    a.addEventListener("click", function () {
      if (window.matchMedia("(max-width: 900px)").matches) {
        closeNav();
      }
    });
  });

  // Highlight current page in sidebar
  var path = (location.pathname || "").replace(/\\/g, "/");
  var file = path.split("/").pop() || "index.html";
  if (!file || file === "" || file === "blueprint") file = "index.html";

  document.querySelectorAll(".sidebar a[data-page]").forEach(function (a) {
    if (a.getAttribute("data-page") === file) {
      a.classList.add("is-active");
    }
  });

  // TOC highlight for on-page anchors (blueprint)
  var headings = Array.prototype.slice.call(
    document.querySelectorAll(".content h2[id], .content h3[id]")
  );
  var tocLinks = Array.prototype.slice.call(
    document.querySelectorAll(".sidebar__sub a[href*='#']")
  );

  function setActiveToc() {
    if (!headings.length || !tocLinks.length) return;
    var scrollY = window.scrollY + 80;
    var current = headings[0];
    for (var i = 0; i < headings.length; i++) {
      if (headings[i].offsetTop <= scrollY) current = headings[i];
    }
    var id = current && current.id;
    tocLinks.forEach(function (link) {
      var href = link.getAttribute("href") || "";
      var hash = href.split("#")[1];
      link.classList.toggle("is-active", hash === id);
    });
  }

  if (headings.length && tocLinks.length) {
    window.addEventListener("scroll", setActiveToc, { passive: true });
    setActiveToc();
  }

  // Simple in-page search / highlight
  if (search) {
    var content = document.querySelector(".content");
    search.addEventListener("input", function () {
      var q = (search.value || "").trim().toLowerCase();
      if (!content) return;

      // Remove previous marks
      content.querySelectorAll("mark.mark").forEach(function (m) {
        var parent = m.parentNode;
        parent.replaceChild(document.createTextNode(m.textContent), m);
        parent.normalize();
      });

      if (q.length < 2) return;

      var walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT, {
        acceptNode: function (node) {
          if (!node.parentElement) return NodeFilter.FILTER_REJECT;
          var tag = node.parentElement.tagName;
          if (tag === "SCRIPT" || tag === "STYLE" || tag === "MARK") {
            return NodeFilter.FILTER_REJECT;
          }
          if (!node.nodeValue || !node.nodeValue.toLowerCase().includes(q)) {
            return NodeFilter.FILTER_SKIP;
          }
          return NodeFilter.FILTER_ACCEPT;
        },
      });

      var nodes = [];
      while (walker.nextNode()) nodes.push(walker.currentNode);

      nodes.forEach(function (textNode) {
        var text = textNode.nodeValue;
        var lower = text.toLowerCase();
        var idx = lower.indexOf(q);
        if (idx < 0) return;
        var before = text.slice(0, idx);
        var match = text.slice(idx, idx + q.length);
        var after = text.slice(idx + q.length);
        var frag = document.createDocumentFragment();
        if (before) frag.appendChild(document.createTextNode(before));
        var mark = document.createElement("mark");
        mark.className = "mark";
        mark.textContent = match;
        frag.appendChild(mark);
        if (after) frag.appendChild(document.createTextNode(after));
        textNode.parentNode.replaceChild(frag, textNode);
      });
    });
  }

  // Mermaid
  if (window.mermaid) {
    window.mermaid.initialize({
      startOnLoad: true,
      theme: "neutral",
      securityLevel: "loose",
      flowchart: { htmlLabels: true, curve: "basis" },
    });
  }
})();
