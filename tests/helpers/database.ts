import EmbeddedPostgres from 'embedded-postgres';
import { PrismaClient } from '@prisma/client';
import { createServer } from 'node:net';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
export async function freePort() { const server=createServer(); await new Promise<void>(r=>server.listen(0,'127.0.0.1',r)); const a=server.address(); if(!a||typeof a==='string') throw new Error('No port'); await new Promise<void>(r=>server.close(()=>r())); return a.port; }
export async function startDatabase() {
 const directory=await mkdtemp(join(tmpdir(),'cs-pg-')); const port=await freePort();
 const pg=new EmbeddedPostgres({databaseDir:join(directory,'data'),port,user:'contextspeak',password:'contextspeak',persistent:true,postgresFlags:['-h','127.0.0.1','-k',directory],onLog:()=>{},onError:()=>{}});
 await pg.initialise(); await pg.start(); await pg.createDatabase('contextspeak');
 const url=`postgresql://contextspeak:contextspeak@127.0.0.1:${port}/contextspeak`;
 execFileSync(process.execPath,['node_modules/prisma/build/index.js','migrate','deploy','--schema','apps/api/prisma/schema.prisma'],{env:{...process.env,DATABASE_URL:url},stdio:'pipe'});
 const prisma=new PrismaClient({datasourceUrl:url}); await prisma.$connect();
 return {prisma,url,close:async()=>{await prisma.$disconnect();await pg.stop();await rm(directory,{recursive:true,force:true});}};
}
