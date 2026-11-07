import { browser } from 'wxt/browser';
import type { CollectionSettings } from '@contextspeak/contracts';
export const API='http://127.0.0.1:4318';
export interface Connection { token:string; identity:string; enabled:boolean; excludedOrigins:string[] }
export const disconnected:Connection={token:'',identity:'',enabled:false,excludedOrigins:[]};
export async function connection():Promise<Connection>{return ((await browser.storage.local.get('connection')).connection as Connection|undefined)??disconnected;}
export async function api<T>(path:string,method='GET',body?:unknown,token?:string):Promise<T>{const auth=token??(await connection()).token;const response=await fetch(API+path,{method,headers:{Authorization:`Bearer ${auth}`,...(body===undefined?{}:{'Content-Type':'application/json'})},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(5000)});if(!response.ok)throw Object.assign(new Error(`Request failed (${response.status})`),{status:response.status});return response.json();}
export async function verifiedIdentity(token:string){const result=await api<{issuer:string;subject:string}>('/v1/identity','GET',undefined,token);return JSON.stringify([result.issuer,result.subject]);}
export async function serverSettings(){return api<CollectionSettings>('/v1/settings');}
