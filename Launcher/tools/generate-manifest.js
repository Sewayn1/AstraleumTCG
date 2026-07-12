#!/usr/bin/env node
/**
 * Génère manifest.json pour une release Astraleum.
 *
 * Usage:
 *   node tools/generate-manifest.js \
 *     --dir   "C:/GameBuild"                               (dossier du build Unity)
 *     --version  0.2.0                                     (nouvelle version)
 *     --release-url  "https://github.com/Sewayn1/AstraleumTCG/releases/download/v0.2.0"
 *     --raw-base "https://raw.githubusercontent.com/Sewayn1/AstraleumTCG/main/gamebuild"
 *                                                           (optionnel, défaut ci-dessous — base
 *                                                            URL des fichiers individuels servis
 *                                                            depuis le repo Git, pour les mises à
 *                                                            jour différentielles)
 *     --title  "Équilibrage des cartes"                    (optionnel)
 *     --notes  "Fix crash|Istan: 15→12|Gobelin: coût 3→2" (optionnel, séparés par |)
 *     --out   "C:/Dev/AstraleumTCG/manifest.json"          (optionnel, défaut: ./manifest.json)
 *
 * IMPORTANT : les fichiers de --dir doivent aussi être copiés dans le dossier "gamebuild/" du
 * repo (même contenu, écrase la version précédente) puis commit+push AVANT de publier ce
 * manifest — sinon les URLs raw.githubusercontent.com pointent vers du contenu absent/périmé.
 * Les mises à jour différentielles téléchargent chaque fichier changé individuellement depuis
 * ce dossier ; --release-url/le ZIP complet ne sert plus qu'au tout premier install.
 */

const fs     = require('fs');
const path   = require('path');
const crypto = require('crypto');

/* ── CLI args ──────────────────────────────────────────────────────────── */
const argv = process.argv.slice(2);
const get  = (flag) => { const i = argv.indexOf(flag); return i >= 0 ? argv[i + 1] : null; };

const buildDir   = get('--dir');
const version    = get('--version');
const releaseUrl = get('--release-url');
const rawBase    = get('--raw-base') || 'https://raw.githubusercontent.com/Sewayn1/AstraleumTCG/main/gamebuild';
const title      = get('--title') || 'Nouvelle version';
const notesRaw   = get('--notes');
const outPath    = get('--out') || path.join(process.cwd(), 'manifest.json');

if (!buildDir || !version || !releaseUrl) {
  console.error('');
  console.error('Usage: node tools/generate-manifest.js --dir <build> --version <x.y.z> --release-url <url>');
  console.error('       [--raw-base <url>] [--title "titre"] [--notes "note1,note2"] [--out manifest.json]');
  console.error('');
  process.exit(1);
}

const notes = notesRaw ? notesRaw.split('|').map(s => s.trim()).filter(Boolean) : ['Voir les notes de patch'];

/* ── Scan files ────────────────────────────────────────────────────────── */
const SKIP_PATTERNS = [/\.pdb$/, /UnityCrashHandler/, /\.(log|tmp)$/];

function scanDir(dir, base = dir, list = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    const rel  = path.relative(base, full).replace(/\\/g, '/');
    if (entry.isDirectory()) {
      scanDir(full, base, list);
    } else if (!SKIP_PATTERNS.some(p => p.test(rel))) {
      list.push(rel);
    }
  }
  return list;
}

function sha256(filePath) {
  const data = fs.readFileSync(filePath);
  return crypto.createHash('sha256').update(data).digest('hex');
}

/* ── Build manifest ────────────────────────────────────────────────────── */
console.log(`\nScanning: ${buildDir}`);
const relPaths = scanDir(buildDir);
console.log(`Found ${relPaths.length} files — computing SHA256…`);

const files = relPaths.map((rel, i) => {
  if ((i + 1) % 10 === 0) process.stdout.write(`  ${i + 1}/${relPaths.length}\r`);
  const full = path.join(buildDir, rel.replace(/\//g, path.sep));
  return {
    path: rel,
    sha256: sha256(full),
    size: fs.statSync(full).size,
    // raw.githubusercontent.com supporte nativement les sous-dossiers (contrairement aux assets
    // GitHub Releases, qui rejettent les "/" dans un nom de fichier — testé, 404 confirmé).
    url: `${rawBase.replace(/\/$/, '')}/${rel.split('/').map(encodeURIComponent).join('/')}`,
  };
});
console.log(`\nHashed ${files.length} files.`);

/* ── Preserve existing changelog ───────────────────────────────────────── */
let existingChangelog = [];
if (fs.existsSync(outPath)) {
  try {
    existingChangelog = JSON.parse(fs.readFileSync(outPath, 'utf8')).changelog || [];
  } catch {}
}

const today     = new Date().toISOString().split('T')[0];
const newEntry  = { version, date: today, title, entries: notes };
const changelog = [newEntry, ...existingChangelog.filter(e => e.version !== version)];

const manifest = {
  version,
  releaseDate: today,
  fullPackage: {
    url:  `${releaseUrl.replace(/\/$/, '')}/Astraleum-v${version}.zip`,
    size: 0,
  },
  files,
  changelog,
};

fs.writeFileSync(outPath, JSON.stringify(manifest, null, 2), 'utf8');

/* ── Summary ───────────────────────────────────────────────────────────── */
const totalMb = (files.reduce((s, f) => s + f.size, 0) / 1048576).toFixed(1);
console.log('');
console.log(`manifest.json → ${outPath}`);
console.log(`  Version  : ${version}`);
console.log(`  Fichiers : ${files.length}  (${totalMb} Mo au total)`);
console.log('');
console.log('Prochaines étapes :');
console.log(`  1. Créer le ZIP complet (sert uniquement au premier install) : Astraleum-v${version}.zip depuis ${buildDir}`);
console.log(`  2. Téléverser le ZIP sur GitHub Release :`);
console.log(`       gh release create ${version} --title "v${version}" Astraleum-v${version}.zip`);
console.log(`  3. Copier ${buildDir} → gamebuild/ à la racine du repo (écrase la version précédente)`);
console.log(`  4. Committer + pousser gamebuild/ ET manifest.json ensemble (sinon les mises à jour`);
console.log(`     différentielles pointeraient vers du contenu absent/périmé) :`);
console.log(`       git add gamebuild manifest.json && git commit -m "release v${version}" && git push`);
console.log('');
