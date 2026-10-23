/** Reçu imprimable : logo, identité de l'entreprise, signature et QR code de vérification (fournis par l'API). */
export function imprimerRecu(r: any) {
  const money = (v: number) => (v ?? 0).toLocaleString('fr-FR', { minimumFractionDigits: 2 }) + ' DA';
  const esc = (t: any) => String(t ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' } as any)[c]);
  const b = r.branding ?? {};
  const s = b.societe ?? {};
  const contact = [s.adresse, s.telephone, s.email].filter((x: any) => !!x).map(esc).join(' &nbsp;•&nbsp; ');

  const w = window.open('', '_blank', 'width=760,height=950');
  if (!w) { alert('Autorisez les fenêtres pop-up pour imprimer.'); return; }
  w.document.write(`<html><head><title>${esc(r.numeroRecu)}</title><style>
    body{font-family:Arial,sans-serif;padding:28px;max-width:680px;margin:auto;color:#1c2733}
    .tete{display:flex;align-items:center;gap:16px;border-bottom:2px solid #1f4e79;padding-bottom:12px;margin-bottom:18px}
    .logo{max-height:70px;max-width:120px;object-fit:contain}
    .soc b{font-size:18px} .soc div{font-size:12px;color:#444;margin-top:2px}
    h1{text-align:center;margin:8px 0 2px;font-size:22px} .n{text-align:center;color:#555;margin-bottom:20px}
    table{width:100%;border-collapse:collapse} td{padding:8px;border-bottom:1px solid #ddd} td:first-child{color:#555;width:40%}
    .m{font-size:20px;font-weight:bold} .annule{color:#c62828;font-weight:bold;text-align:center;font-size:18px;margin:6px 0}
    .bas{display:flex;justify-content:space-between;align-items:flex-end;margin-top:36px;gap:20px}
    .qr{display:flex;gap:10px;align-items:center;max-width:62%} .qr img{width:96px;height:96px}
    .qr div{font-size:10px;color:#555;word-break:break-all} .sign{text-align:center;min-width:190px}
    .sign img{max-height:80px;max-width:190px;display:block;margin:4px auto} .vide{height:70px}
    .pied{margin-top:22px;border-top:1px solid #ccc;padding-top:6px;font-size:10px;color:#666;text-align:center}
  </style></head><body>
    <div class="tete">
      ${b.logo ? `<img class="logo" src="${b.logo}">` : ''}
      <div class="soc"><b>${esc(s.nom ?? '')}</b>
        ${contact ? `<div>${contact}</div>` : ''}${s.identifiants ? `<div>${esc(s.identifiants)}</div>` : ''}</div>
    </div>
    <h1>REÇU DE PAIEMENT</h1><div class="n">N° ${esc(r.numeroRecu)}</div>
    <table>
      <tr><td>Date</td><td>${new Date(r.date).toLocaleString('fr-FR')}</td></tr>
      <tr><td>Reçu de</td><td>${esc(r.client)}</td></tr>
      <tr><td>Chantier / Lot</td><td>${esc(r.chantier)} — ${esc(r.lot)}</td></tr>
      <tr><td>Mode de paiement</td><td>${r.mode === 'Especes' ? 'Espèces' : 'Bancaire'}${r.refBancaire ? ' (' + esc(r.refBancaire) + ')' : ''}</td></tr>
      <tr><td>Montant encaissé</td><td class="m">${money(r.montant)}</td></tr>
      <tr><td>Prix total</td><td>${money(r.prixTotal)}</td></tr>
      <tr><td>Total payé à ce jour</td><td>${money(r.totalPaye)}</td></tr>
      <tr><td>Reste à payer</td><td>${money(r.reste)}</td></tr>
      <tr><td>Encaissé par</td><td>${esc(r.agent)}</td></tr>
    </table>
    <div class="bas">
      <div class="qr">${b.qr ? `<img src="${b.qr}">` : ''}
        <div><b>Vérifier ce reçu</b><br>Code : ${esc(b.code)}<br>Scannez le QR code pour contrôler son authenticité.</div></div>
      <div class="sign"><b>${esc(b.signataireFonction ?? 'Le Gérant')}</b>
        ${b.signature ? `<img src="${b.signature}">` : '<div class="vide"></div>'}
        ${b.signataire ? `<div>${esc(b.signataire)}</div>` : ''}</div>
    </div>
    ${s.piedDePage ? `<div class="pied">${esc(s.piedDePage)}</div>` : ''}
    <script>window.onload=()=>setTimeout(()=>window.print(),200)</script></body></html>`);
  w.document.close();
}
