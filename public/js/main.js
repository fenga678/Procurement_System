document.addEventListener('DOMContentLoaded', () => {
  // 移动端菜单切换
  const toggle = document.querySelector('.nav-toggle');
  const links = document.querySelector('.nav-links');
  if (toggle && links) {
    toggle.addEventListener('click', () => links.classList.toggle('open'));
    links.querySelectorAll('a').forEach(a => a.addEventListener('click', () => links.classList.remove('open')));
  }

  // FAQ 折叠
  document.querySelectorAll('.faq-q').forEach(q => {
    q.addEventListener('click', () => q.parentElement.classList.toggle('open'));
  });

  // Lightbox 灯箱
  const overlay = document.querySelector('.lightbox-overlay');
  const lbImg = overlay ? overlay.querySelector('img') : null;
  document.querySelectorAll('[data-lightbox]').forEach(el => {
    el.style.cursor = 'zoom-in';
    el.addEventListener('click', e => {
      e.preventDefault();
      if (lbImg) lbImg.src = el.href || el.src;
      overlay.classList.add('active');
      document.body.style.overflow = 'hidden';
    });
  });
  function closeLightbox() {
    overlay.classList.remove('active');
    document.body.style.overflow = '';
  }
  if (overlay) {
    overlay.addEventListener('click', e => { if (e.target === overlay || e.target.closest('.lightbox-close')) closeLightbox(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeLightbox(); });
  }
});
