import { beforeAll, afterAll, describe, expect, it } from 'vitest';
import request from 'supertest';
import { createApp } from '../src/app';
import { startDatabase } from '../../../tests/helpers/database';
import { startIssuer } from '../../../tests/helpers/issuer';
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
beforeAll(async () => { issuer = await startIssuer(); db = await startDatabase(); app = await createApp({ issuer: issuer.issuer, audience: issuer.audience, jwksUrl: issuer.jwksUrl, prisma: db.prisma }); });
afterAll(async () => { await app?.close(); await issuer?.close(); await db?.close(); });
describe('authenticated API boundary', () => {
 it('rejects_missing_expired_wrong_audience_and_unsigned_tokens', async () => {
  const unsigned = Buffer.from(JSON.stringify({ alg: 'none' })).toString('base64url')+'.'+Buffer.from(JSON.stringify({sub:'learner-a',iss:issuer.issuer,aud:issuer.audience})).toString('base64url')+'.';
  const tokens = ['', await issuer.token('learner-a',{expires:0}), await issuer.token('learner-a',{audience:'other'}), await issuer.token('learner-a',{issuer:'https://wrong.invalid'}), unsigned];
  for (const token of tokens) { const call=request(app.getHttpServer()).get('/v1/lookups'); if(token) call.set('authorization',`Bearer ${token}`); expect((await call).status).toBe(401); }
 });
 it('accepts_a_verified_identity', async () => { const result=await request(app.getHttpServer()).get('/v1/lookups').set('authorization',`Bearer ${await issuer.token()}`); expect(result.status).toBe(200); });
});
