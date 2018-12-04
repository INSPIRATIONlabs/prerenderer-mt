let ssrId = 0;
const setSsrV = function (el) {
  if (el && el.classList.contains('hydrated')) {
    el.setAttribute('ssrv', ssrId++);
  }
  for (const child of el.children) {
    setSsrV(child);
  }
};

for (const child of document.body.children) {
  setSsrV(child);
}

for (const element of document.body.getElementsByTagName('*')) {
  const parent = element.parentElement.closest('.hydrated');
  const parentId = parent ? parent.getAttribute('ssrv') : null;
  if (parentId) {
    const childIdx = Array.from(element.parentElement.children).findIndex((entry) => entry === element);
    element.setAttribute('ssrc', [parentId, childIdx].join('.'));
  }
}

document.querySelector('html').setAttribute('ssr', new Date().toISOString());
