import { lookupSchema, type LookupInput, type LookupReceipt, type CollectionSettings } from '@contextspeak/contracts';
export interface OutboxRecord {input:LookupInput;identity:string;status:'pending'|'paused'|'synchronized'|'failed';attempts:number;receipt?:LookupReceipt;error?:string}
export interface OutboxState {records:OutboxRecord[]}
interface Dependencies {
 storage:{read():Promise<OutboxState>;write(value:OutboxState):Promise<void>};
 settings():Promise<CollectionSettings&{identity:string}>;
 remoteSettings():Promise<CollectionSettings>;
 send(input:LookupInput):Promise<LookupReceipt>;
}
export class Outbox {
 private operation:Promise<unknown>=Promise.resolve();
 constructor(private deps:Dependencies){}
 private serialize<T>(fn:()=>Promise<T>):Promise<T>{const next=this.operation.then(fn);this.operation=next.catch(()=>{});return next;}
 enqueueLookup(value:LookupInput){return this.serialize(async()=>{const input=lookupSchema.parse(value);const settings=await this.deps.settings();if(!this.allowed(input,settings)||!settings.identity)return;const state=await this.deps.storage.read();if(state.records.some(r=>r.identity===settings.identity&&r.input.eventId===input.eventId))return;if(state.records.filter(r=>r.status!=='synchronized').length>=200)throw new Error('Outbox is full. Synchronize or discard queued queries first.');state.records=state.records.filter(r=>r.status!=='synchronized').concat(state.records.filter(r=>r.status==='synchronized').slice(-20));state.records.push({input,identity:settings.identity,status:'pending',attempts:0});await this.deps.storage.write(state);});}
 flushOutbox(){return this.serialize(async()=>{
  let state=await this.deps.storage.read();
  for(const record of [...state.records]){
   if(record.status==='synchronized'||record.status==='failed')continue;
   const settings=await this.deps.settings();if(record.identity!==settings.identity)continue;
   if(!this.allowed(record.input,settings)){state.records=state.records.filter(r=>r!==record);await this.deps.storage.write(state);continue;}
   try{
    const remote=await this.deps.remoteSettings();
    const latest=await this.deps.settings();
    if(latest.identity!==record.identity)break;
    if(!this.allowed(record.input,latest)||!this.allowed(record.input,remote)){state.records=state.records.filter(r=>r!==record);await this.deps.storage.write(state);continue;}
    record.receipt=await this.deps.send(record.input);record.status='synchronized';delete record.error;
   }catch(e){const status=(e as {status?:number}).status;record.error=status===401?'Reconnect with a fresh developer token.':status===409?'Event conflict. Discard this query.':'Synchronization unavailable.';if(status===403){state.records=state.records.filter(r=>r!==record);}else if(status===401){record.status='paused';}else{record.attempts++;record.status=status&&status>=400&&status<500||record.attempts>=5?'failed':'pending';}await this.deps.storage.write(state);break;}
   await this.deps.storage.write(state);
  }
 });}
 private allowed(input:LookupInput,settings:CollectionSettings){return settings.enabled&&(!input.sourceUrl||!settings.excludedOrigins.includes(new URL(input.sourceUrl).origin));}
}
