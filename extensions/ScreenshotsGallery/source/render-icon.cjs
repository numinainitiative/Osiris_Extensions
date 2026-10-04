const fs = require('fs');
const path = require('path');
const sharp = require(process.env.OSIRIS_SHARP_MODULE || 'sharp');
async function render() {
    const svg = fs.readFileSync(path.join(__dirname, 'fullscreen.svg'), 'utf8')
        .replace('stroke="currentColor"', 'stroke="#F0F1F7"');
    const glyph = await sharp(Buffer.from(svg), { density: 768 }).resize(210, 210).png().toBuffer();
    await sharp({ create: { width: 256, height: 256, channels: 4, background: '#070707' } })
        .composite([{ input: glyph, left: 23, top: 23 }]).png()
        .toFile(path.join(__dirname, 'icon.png'));
}
render().catch(error => { console.error(error); process.exitCode = 1; });
