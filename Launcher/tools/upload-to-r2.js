#!/usr/bin/env node
/**
 * Synchronise un dossier local (ex. gamebuild/) vers un bucket Cloudflare R2.
 * Remplace le "git add gamebuild && git commit && git push" du workflow de release —
 * R2 n'a pas de limite de taille de fichier ni de coût d'egress, contrairement à Git/GitHub.
 *
 * Credentials lues depuis Launcher/.env (jamais committé, voir .env.example) :
 *   R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME, R2_PUBLIC_URL
 *
 * Usage:
 *   node tools/upload-to-r2.js --dir "G:/Unity/Project/Astraleum/gamebuild" [--prefix gamebuild]
 */

const fs     = require('fs');
const path   = require('path');
const crypto = require('crypto');
require('dotenv').config({ path: path.join(__dirname, '..', '.env') });

const { S3Client, PutObjectCommand, HeadObjectCommand } = require('@aws-sdk/client-s3');

/* ── CLI args ──────────────────────────────────────────────────────────── */
const argv = process.argv.slice(2);
const get  = (flag, def = null) => { const i = argv.indexOf(flag); return i >= 0 ? argv[i + 1] : def; };

const sourceDir = get('--dir');
const prefix    = get('--prefix', ''); // préfixe optionnel dans le bucket (ex. "gamebuild")

if (!sourceDir) {
  console.error('\nUsage: node tools/upload-to-r2.js --dir <dossier> [--prefix <prefixe>]\n');
  process.exit(1);
}

const { R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME, R2_PUBLIC_URL } = process.env;
const missing = ['R2_ACCOUNT_ID', 'R2_ACCESS_KEY_ID', 'R2_SECRET_ACCESS_KEY', 'R2_BUCKET_NAME', 'R2_PUBLIC_URL']
  .filter((k) => !process.env[k]);
if (missing.length > 0) {
  console.error(`\nVariables manquantes dans Launcher/.env : ${missing.join(', ')}\n`);
  process.exit(1);
}

const s3 = new S3Client({
  region: 'auto',
  endpoint: `https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com`,
  credentials: {
    accessKeyId: R2_ACCESS_KEY_ID,
    secretAccessKey: R2_SECRET_ACCESS_KEY,
  },
});

/* ── Scan ──────────────────────────────────────────────────────────────── */
function scanDir(dir, base = dir, list = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) scanDir(full, base, list);
    else list.push(path.relative(base, full).replace(/\\/g, '/'));
  }
  return list;
}

function contentType(rel) {
  const ext = path.extname(rel).toLowerCase();
  return {
    '.mp4': 'video/mp4', '.mp3': 'audio/mpeg', '.png': 'image/png',
    '.json': 'application/json', '.txt': 'text/plain',
  }[ext] || 'application/octet-stream';
}

function md5(filePath) {
  return crypto.createHash('md5').update(fs.readFileSync(filePath)).digest('hex');
}

// Objet déjà présent sur R2 avec le même contenu → on saute (évite de retransférer les
// centaines de Mo inchangés à chaque release, seuls les fichiers modifiés sont réuploadés).
// Compare l'ETag (MD5 du contenu pour un PutObject simple, non multipart) plutôt que la seule
// taille — un bug précédent (taille identique mais contenu différent, ex. bump de version dans
// globalgamemanagers) faisait passer un fichier modifié pour "déjà à jour" et resservait
// silencieusement l'ancien contenu depuis R2.
async function alreadyUploaded(key, localPath) {
  try {
    const head = await s3.send(new HeadObjectCommand({ Bucket: R2_BUCKET_NAME, Key: key }));
    const remoteEtag = (head.ETag || '').replace(/"/g, '');
    if (remoteEtag.includes('-')) return false; // ETag multipart (non-MD5) → pas fiable, on réuploade par sécurité
    return remoteEtag === md5(localPath);
  } catch {
    return false; // 404 ou autre erreur → pas encore présent, on upload
  }
}

/* ── Main ──────────────────────────────────────────────────────────────── */
(async () => {
  console.log(`\nScanning: ${sourceDir}`);
  const relPaths = scanDir(sourceDir);
  console.log(`Found ${relPaths.length} files.\n`);

  let uploaded = 0, skipped = 0, totalBytes = 0;

  for (let i = 0; i < relPaths.length; i++) {
    const rel = relPaths[i];
    const full = path.join(sourceDir, rel.replace(/\//g, path.sep));
    const key = prefix ? `${prefix}/${rel}` : rel;
    const size = fs.statSync(full).size;

    process.stdout.write(`[${i + 1}/${relPaths.length}] ${rel} `);

    if (await alreadyUploaded(key, full)) {
      console.log('(déjà à jour, ignoré)');
      skipped++;
      continue;
    }

    await s3.send(new PutObjectCommand({
      Bucket: R2_BUCKET_NAME,
      Key: key,
      Body: fs.createReadStream(full),
      ContentLength: size,
      ContentType: contentType(rel),
    }));
    console.log(`(${(size / 1048576).toFixed(1)} Mo, envoyé)`);
    uploaded++;
    totalBytes += size;
  }

  console.log(`\nTerminé : ${uploaded} fichiers envoyés (${(totalBytes / 1048576).toFixed(1)} Mo), ${skipped} déjà à jour.`);
  console.log(`URL publique de base : ${R2_PUBLIC_URL.replace(/\/$/, '')}/${prefix}\n`);
})().catch((err) => {
  console.error('\nErreur upload R2 :', err.message || err);
  process.exit(1);
});
