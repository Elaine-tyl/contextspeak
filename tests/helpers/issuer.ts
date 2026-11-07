import { createServer } from 'node:http';
import { generateKeyPair, exportJWK, SignJWT } from 'jose';
export async function startIssuer(port = 0) {
 const { privateKey, publicKey } = await generateKeyPair('RS256');
 const jwk = { ...await exportJWK(publicKey), kid: 'local-test', alg: 'RS256', use: 'sig' };
 const server = createServer((req,res) => { if (req.url !== '/.well-known/jwks.json') { res.writeHead(404).end(); return; } res.setHeader('content-type','application/json'); res.end(JSON.stringify({ keys: [jwk] })); });
 await new Promise<void>((resolve) => server.listen(port, '127.0.0.1', resolve));
 const address = server.address(); if (!address || typeof address === 'string') throw new Error('Issuer address unavailable');
 const issuer = `http://127.0.0.1:${address.port}`;
 return { issuer, jwksUrl: `${issuer}/.well-known/jwks.json`, audience: 'contextspeak-local',
  token: (subject='learner-a', options: { audience?: string; expires?: string | number; issuer?: string } = {}) => new SignJWT({}).setProtectedHeader({ alg: 'RS256', kid: 'local-test' }).setIssuer(options.issuer ?? issuer).setSubject(subject).setAudience(options.audience ?? 'contextspeak-local').setIssuedAt().setExpirationTime(options.expires ?? '1h').sign(privateKey),
  close: () => new Promise<void>((resolve,reject) => server.close(err => err ? reject(err) : resolve())) };
}
