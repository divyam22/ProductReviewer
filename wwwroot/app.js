/* ═══════════════════════════════════════════════════════════
   SentimentIQ — Frontend Application Logic
   ═══════════════════════════════════════════════════════════ */

'use strict';

// ── State ─────────────────────────────────────────────────
let currentData = null;
let allReviews   = [];
let visibleCount = 12;
let activeFilter = 'all';

// ── DOM Refs ──────────────────────────────────────────────
const $ = id => document.getElementById(id);

const heroSection    = $('hero');
const loadingSection = $('loading-section');
const resultsSection = $('results-section');
const errorSection   = $('error-section');
const productInput   = $('product-input');
const searchBtn      = $('search-btn');

// ── Source Styles ─────────────────────────────────────────
const SOURCE_STYLES = {
  'Reddit':            { color: '#FF4500', bg: 'rgba(255,69,0,0.12)',   label: '🔴' },
  'Google Play Store': { color: '#34A853', bg: 'rgba(52,168,83,0.12)',  label: '▶' },
  'Amazon':            { color: '#FF9900', bg: 'rgba(255,153,0,0.12)',  label: '📦' },
  'Trustpilot':        { color: '#00B67A', bg: 'rgba(0,182,122,0.12)', label: '★' },
  'G2':                { color: '#FF492C', bg: 'rgba(255,73,44,0.12)',  label: 'G2' },
};

function getSourceStyle(source) {
  for (const [key, style] of Object.entries(SOURCE_STYLES)) {
    if (source.startsWith(key)) return { ...style, name: key };
  }
  return { color: '#6366f1', bg: 'rgba(99,102,241,0.12)', label: '🌐', name: source };
}

// ── Entry Points ──────────────────────────────────────────
function quickSearch(query) {
  productInput.value = query;
  runAnalysis();
}

async function runAnalysis() {
  const query = productInput.value.trim();
  if (!query) {
    shakify(productInput.closest('.search-bar'));
    productInput.focus();
    return;
  }

  showSection('loading');
  animateLoadingSteps();

  try {
    const res = await fetch(`/api/reviews/analyze?product=${encodeURIComponent(query)}`);
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: 'Server error. Please try again.' }));
      showError(err.error || 'Failed to fetch reviews.', '');
      return;
    }
    const data = await res.json();
    currentData = data;
    allReviews   = data.reviews || [];
    visibleCount = 12;
    activeFilter = 'all';
    renderResults(data);
    showSection('results');
  } catch (e) {
    console.error(e);
    showError('Network error — could not reach the server.', 'Make sure the API is running on http://localhost:5000');
  }
}

function resetToSearch() {
  showSection('hero');
  productInput.value = '';
  productInput.focus();
}

// ── Keyboard ──────────────────────────────────────────────
productInput.addEventListener('keydown', e => {
  if (e.key === 'Enter') runAnalysis();
});

// ── Section Switch ────────────────────────────────────────
function showSection(name) {
  heroSection.style.display    = name === 'hero'    ? 'flex'  : 'none';
  loadingSection.style.display = name === 'loading' ? 'flex'  : 'none';
  resultsSection.style.display = name === 'results' ? 'block' : 'none';
  errorSection.style.display   = name === 'error'   ? 'flex'  : 'none';
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

function showError(msg, detail) {
  $('error-message').textContent = msg;
  $('error-detail').textContent  = detail;
  showSection('error');
}

// ── Loading Animation ─────────────────────────────────────
function animateLoadingSteps() {
  const query = productInput.value.trim();
  $('loading-product').textContent = query;

  const steps = ['step-reddit','step-amazon','step-playstore','step-web','step-ai'];
  steps.forEach(id => {
    const el = $(id);
    el.classList.remove('active','done');
  });

  let i = 0;
  function next() {
    if (i > 0) $(steps[i-1]).classList.replace('active','done');
    if (i < steps.length) {
      $(steps[i]).classList.add('active');
      i++;
      setTimeout(next, 1200 + Math.random() * 600);
    }
  }
  next();
}

// ── Render Results ────────────────────────────────────────
function renderResults(data) {
  // Basic info
  $('result-product').textContent     = data.productQuery;
  $('result-rating-label').textContent = data.overallRating;
  $('result-meta').textContent        =
    `${data.totalReviews} reviews analyzed from ${data.sourceSummaries?.length || 0} sources`;

  // Star rating
  $('result-stars').innerHTML = buildStars(data.overallScore);

  // SVG gradient (inject once)
  injectSVGGradient();

  // Score ring animation
  animateRing(data.overallScore);

  // Sentiment bars
  animateBars(data.positivePercent, data.neutralPercent, data.negativePercent);

  // Key themes
  renderThemes(data.keyThemes || []);

  // Source cards
  renderSourceCards(data.sourceSummaries || []);

  // Reviews
  renderReviewCards();

  // Footer
  const ts = new Date(data.analyzedAt);
  $('analysis-footer').textContent =
    `Analysis completed at ${ts.toLocaleTimeString()} on ${ts.toLocaleDateString()}`;
}

// ── Stars ─────────────────────────────────────────────────
function buildStars(score) {
  const full  = Math.floor(score);
  const half  = score - full >= 0.4 ? 1 : 0;
  const empty = 5 - full - half;
  return '⭐'.repeat(full) + (half ? '✨' : '') + ('☆'.repeat(empty));
}

// ── SVG Ring ─────────────────────────────────────────────
function injectSVGGradient() {
  const svg = document.querySelector('.ring-svg');
  if (!svg || svg.querySelector('defs')) return;
  const defs = document.createElementNS('http://www.w3.org/2000/svg','defs');
  defs.innerHTML = `
    <linearGradient id="ringGrad" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%"   stop-color="#6366f1"/>
      <stop offset="50%"  stop-color="#a78bfa"/>
      <stop offset="100%" stop-color="#38bdf8"/>
    </linearGradient>`;
  svg.prepend(defs);
}

function animateRing(score) {
  const pct = score / 5.0;
  const circumference = 2 * Math.PI * 80; // r=80
  const offset = circumference * (1 - pct);
  const fill = $('ring-fill');
  const scoreEl = $('ring-score');

  // Reset
  fill.style.strokeDashoffset = circumference;
  scoreEl.textContent = '0.0';

  // Animate after paint
  requestAnimationFrame(() => {
    setTimeout(() => {
      fill.style.strokeDashoffset = offset;

      // Counter animation
      const start = performance.now();
      const duration = 1500;
      function tick(now) {
        const t = Math.min((now - start) / duration, 1);
        const ease = 1 - Math.pow(1 - t, 3);
        scoreEl.textContent = (score * ease).toFixed(1);
        if (t < 1) requestAnimationFrame(tick);
        else scoreEl.textContent = score.toFixed(1);
      }
      requestAnimationFrame(tick);
    }, 100);
  });
}

// ── Sentiment Bars ────────────────────────────────────────
function animateBars(pos, neu, neg) {
  setTimeout(() => {
    $('bar-positive').style.width = pos + '%';
    $('bar-neutral').style.width  = neu + '%';
    $('bar-negative').style.width = neg + '%';
    $('pct-positive').textContent = pos + '%';
    $('pct-neutral').textContent  = neu + '%';
    $('pct-negative').textContent = neg + '%';
  }, 200);
}

// ── Themes ───────────────────────────────────────────────
const THEME_ICONS = {
  'Battery Life': 'battery_5_bar', 'Performance': 'speed', 'Camera Quality': 'photo_camera',
  'Display/Screen': 'monitor', 'Build Quality': 'construction', 'Price/Value': 'payments',
  'Customer Support': 'support_agent', 'Software/App': 'apps', 'Sound/Audio': 'volume_up',
  'Shipping/Delivery': 'local_shipping', 'Connectivity': 'wifi', 'Design': 'palette',
};

function renderThemes(themes) {
  const el = $('themes-list');
  if (!themes.length) { el.innerHTML = '<span style="color:var(--text-3);font-size:.85rem">No specific themes detected</span>'; return; }
  el.innerHTML = themes.map(t =>
    `<span class="theme-chip">
       <span class="material-icons-round">${THEME_ICONS[t] || 'label'}</span>${t}
     </span>`
  ).join('');
}

// ── Source Cards ──────────────────────────────────────────
function renderSourceCards(sources) {
  const container = $('source-cards');
  if (!sources.length) {
    container.innerHTML = '<p style="color:var(--text-3)">No source data available.</p>';
    return;
  }

  container.innerHTML = sources.map((s, i) => {
    const style  = getSourceStyle(s.source);
    const sentClass = s.sentiment.toLowerCase();
    const stars  = (s.averageScore * 5).toFixed(1);
    return `
      <div class="source-card" style="animation-delay:${i * 0.08}s">
        <div class="source-card-icon" style="background:${style.bg};color:${style.color}">
          ${getSourceIcon(s.source)}
        </div>
        <div class="source-card-name">${s.source}</div>
        <div class="source-card-count">${s.reviewCount} review${s.reviewCount !== 1 ? 's' : ''}</div>
        <div class="source-card-score" style="color:${style.color}">${stars}</div>
        <div class="source-card-sentiment sent-${sentClass}">${s.sentiment}</div>
      </div>`;
  }).join('');
}

function getSourceIcon(source) {
  if (source.startsWith('Reddit'))      return '<span class="material-icons-round" style="font-size:20px">forum</span>';
  if (source.includes('Play Store'))    return '<span class="material-icons-round" style="font-size:20px">android</span>';
  if (source.startsWith('Amazon'))      return '<span class="material-icons-round" style="font-size:20px">shopping_bag</span>';
  if (source.startsWith('Trustpilot'))  return '<span class="material-icons-round" style="font-size:20px">star_rate</span>';
  if (source.startsWith('G2'))          return '<span style="font-size:14px;font-weight:700">G2</span>';
  return '<span class="material-icons-round" style="font-size:20px">language</span>';
}

// ── Review Cards ──────────────────────────────────────────
function filterReviews(filter) {
  activeFilter = filter;
  visibleCount = 12;

  // Update tab styles
  document.querySelectorAll('.filter-tab').forEach(t => t.classList.remove('active'));
  $('tab-' + filter.toLowerCase()).classList.add('active');

  renderReviewCards();
}

function getFilteredReviews() {
  if (activeFilter === 'all') return allReviews;
  return allReviews.filter(r => r.sentiment === activeFilter);
}

function renderReviewCards() {
  const filtered = getFilteredReviews();
  const grid     = $('reviews-grid');
  const loadBtn  = $('load-more-btn');

  if (!filtered.length) {
    grid.innerHTML = `
      <div style="grid-column:1/-1;text-align:center;padding:60px 20px;color:var(--text-3)">
        <span class="material-icons-round" style="font-size:48px;display:block;margin-bottom:12px">search_off</span>
        No ${activeFilter !== 'all' ? activeFilter.toLowerCase() + ' ' : ''}reviews found.
      </div>`;
    loadBtn.style.display = 'none';
    return;
  }

  const shown = filtered.slice(0, visibleCount);
  grid.innerHTML = shown.map((r, i) => buildReviewCard(r, i)).join('');

  loadBtn.style.display = filtered.length > visibleCount ? 'flex' : 'none';
}

function loadMoreReviews() {
  visibleCount += 12;
  renderReviewCards();
}

function buildReviewCard(review, index) {
  const style     = getSourceStyle(review.source);
  const sentClass = (review.sentiment || 'neutral').toLowerCase();
  const sentLabel = getSentimentLabel(review.sentiment);
  const scoreBar  = buildScoreBar(review.sentimentScore || 0.5);

  const footerParts = [];
  if (review.rating) footerParts.push(`<span class="review-rating">${review.rating}</span>`);
  if (review.date)   footerParts.push(`<span>${review.date}</span>`);
  if (review.url)    footerParts.push(`<a href="${review.url}" target="_blank" rel="noopener" class="review-link">View <span class="material-icons-round">open_in_new</span></a>`);

  return `
    <div class="review-card ${sentClass}" style="animation-delay:${(index % 12) * 0.04}s">
      <div class="review-header">
        <div class="review-author-info">
          <span class="review-author">${escapeHtml(review.author || 'Anonymous')}</span>
          <span class="review-source" style="color:${style.color};border-color:${style.color}30;background:${style.bg}">
            ${getSourceIcon(review.source)} ${escapeHtml(review.source)}
          </span>
        </div>
        <span class="review-sentiment-badge badge-${sentClass}">${sentLabel}</span>
      </div>
      <p class="review-text">${escapeHtml(review.text || '')}</p>
      ${scoreBar}
      ${footerParts.length ? `<div class="review-footer">${footerParts.join('<span style="color:var(--border-h)">·</span>')}</div>` : ''}
    </div>`;
}

function buildScoreBar(score) {
  const pct   = Math.round(score * 100);
  const color = score >= 0.6 ? 'var(--positive)' : score <= 0.4 ? 'var(--negative)' : 'var(--neutral)';
  return `
    <div style="display:flex;align-items:center;gap:8px">
      <div style="flex:1;height:4px;background:rgba(255,255,255,0.06);border-radius:99px;overflow:hidden">
        <div style="height:100%;width:${pct}%;background:${color};border-radius:99px;transition:width 1s ease"></div>
      </div>
      <span style="font-size:0.72rem;color:var(--text-3);width:32px;text-align:right">${pct}%</span>
    </div>`;
}

function getSentimentLabel(s) {
  if (!s || s === 'Neutral')  return '😐 Neutral';
  if (s === 'Positive') return '😊 Positive';
  if (s === 'Negative') return '😞 Negative';
  return s;
}

// ── Helpers ───────────────────────────────────────────────
function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function shakify(el) {
  el.style.animation = 'none';
  el.offsetHeight; // reflow
  el.style.animation = 'shake 0.4s ease';
  el.addEventListener('animationend', () => el.style.animation = '', { once: true });
}

// ── Add shake keyframe dynamically ────────────────────────
(function injectShake() {
  const style = document.createElement('style');
  style.textContent = `
    @keyframes shake {
      0%,100%{transform:translateX(0)}
      20%{transform:translateX(-8px)}
      40%{transform:translateX(8px)}
      60%{transform:translateX(-5px)}
      80%{transform:translateX(5px)}
    }`;
  document.head.appendChild(style);
})();

// ── On page load ──────────────────────────────────────────
window.addEventListener('load', () => {
  showSection('hero');
  productInput.focus();

  // Verify API health
  fetch('/api/reviews/health')
    .then(r => r.ok ? r.json() : null)
    .then(d => {
      if (d) console.log('✅ SentimentIQ API is healthy:', d);
    })
    .catch(() => console.warn('⚠️ API health check failed — is the server running?'));
});
