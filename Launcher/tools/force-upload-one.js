#!/usr/bin/env node
// Force-upload d'un seul fichier vers R2, sans vérification de taille (contourne le bug
// de upload-to-r2.js qui compare uniquement ContentLength et peut rater un changement de
// contenu à taille égale, ex. bump de version dans globalgamemanagers).
// Usage: node tools/force-upload-one.js --file <chemin_local> --key <clef_r2>

const fs   = require('fs');
const path = require('path');
require('dotenv').config({ path: path.join(__dirname, '..', '.env') });
const { S3Client, PutObjectCommand } = require('@aws-sdk/client-s3');

const argv = process.argv.slice(2);
const get  = (flag) => { const i = argv.indexOf(flag); return i >= 0 ? argv[i + 1] : null; };

const file = get('--file');
const key  = get('--key');
if (!file || !key) {
  console.error('Usage: node tools/force-upload-one.js --file <chemin_local> --key <clef_r2>');
  process.exit(1);
}

const { R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME } = process.env;

const s3 = new S3Client({
  region: 'auto',
  endpoint: `https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com`,
  credentials: { accessKeyId: R2_ACCESS_KEY_ID, secretAccessKey: R2_SECRET_ACCESS_KEY },
});

(async () => {
  const body = fs.readFileSync(file);
  await s3.send(new PutObjectCommand({
    Bucket: R2_BUCKET_NAME,
    Key: key,
    Body: body,
    ContentType: 'application/octet-stream',
  }));
  console.log(`Envoyé (forcé) : ${key} (${(body.length / 1048576).toFixed(2)} Mo)`);
})();
