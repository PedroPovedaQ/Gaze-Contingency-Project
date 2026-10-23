// Read-only browser capacity measurement for an explicitly supplied synthetic page.
const { chromium }=require('playwright');
const fs=require('node:fs');
const { pathToFileURL }=require('node:url');
const path=require('node:path');
const { performance }=require('node:perf_hooks');
const assert=require('node:assert/strict');
const percentile=(values,p)=>[...values].sort((a,b)=>a-b)[Math.min(values.length-1,Math.floor(values.length*p))];
(async()=>{
 const [html,output]=process.argv.slice(2);
 if(!html||!output||fs.existsSync(output))throw new Error('Supply a synthetic HTML page and a new report path');
 const browser=await chromium.launch({headless:true});
 const errors=[];
 try {
  const page=await browser.newPage({viewport:{width:1280,height:900}});
  page.on('pageerror',error=>errors.push(error.message));
  const cdp=await page.context().newCDPSession(page);await cdp.send('Performance.enable');
  const started=performance.now();await page.goto(pathToFileURL(path.resolve(html)).href,{waitUntil:'load',timeout:120000});
  await page.waitForSelector('#p20-data tr');
  const loadMs=performance.now()-started;
  assert.match(await page.locator('#p20-title').textContent(),/SYNTHETIC/);
  const count=await page.locator('#p20-trial option').count(),switchTimes=[],seekTimes=[];
  for(let trial=0;trial<count;trial++){
   const measurements=await page.evaluate(trial=>{
    const choice=document.querySelector('#p20-trial'),slider=document.querySelector('#p20-time');
    let start=performance.now();choice.value=trial;choice.dispatchEvent(new Event('change'));
    const switched=performance.now()-start,seeks=[];
    for(const fraction of [.1,.75,.4,.95]){start=performance.now();slider.value=Number(slider.max)*fraction;slider.dispatchEvent(new Event('input'));seeks.push(performance.now()-start);}
    const inspected=JSON.parse(document.querySelector('#p20-raw').textContent);
    if(inspected.sample?.round!==Number(trial)||inspected.sample?.sourceTrialId!==`SYNTHETIC_CAPACITY_run001_r${String(trial).padStart(2,'0')}`)throw new Error('Wrong trial sample after switching/scrubbing');
    return {switched,seeks};
   },String(trial));
   switchTimes.push(measurements.switched);seekTimes.push(...measurements.seeks);
  }
  await page.locator('#p20-trial').selectOption('0');await page.locator('#p20-time').fill('2');
  const frames=await page.evaluate(async()=>{
   const times=[];let last=null;
   document.querySelector('#p20-play').click();
   await new Promise(resolve=>{const begin=performance.now();function step(now){if(last!==null)times.push(now-last);last=now;if(now-begin>=3000){resolve();return;}requestAnimationFrame(step);}requestAnimationFrame(step);});
   document.querySelector('#p20-play').click();return times;
  });
  const metrics=Object.fromEntries((await cdp.send('Performance.getMetrics')).metrics.map(m=>[m.name,m.value]));
  assert.deepEqual(errors,[]);
  const result={schema:'synthetic-timeline-capacity-v1',timestamp:new Date().toISOString(),browserVersion:browser.version(),
   htmlBytes:fs.statSync(html).size,trials:count,verifiedTrialIdentities:count,loadMs,
   synchronousTrialSwitchMs:{median:percentile(switchTimes,.5),p95:percentile(switchTimes,.95),max:Math.max(...switchTimes)},
   synchronousSeekMs:{median:percentile(seekTimes,.5),p95:percentile(seekTimes,.95),max:Math.max(...seekTimes)},
   playbackAnimationIntervalMs:{median:percentile(frames,.5),p95:percentile(frames,.95),max:Math.max(...frames),frames:frames.length},
   sampledJSHeapBytes:metrics.JSHeapUsedSize,notes:['Synthetic desktop Chromium check; no headset, eye-rate, audio or video validation.',
   'Synchronous action timings exclude subsequent paint. Animation intervals include playback rendering for three seconds.',
   'Heap sampled after interaction; not peak process memory. Cold OS/browser cache and hardware differ across machines.']};
  fs.writeFileSync(output,JSON.stringify(result,null,2)+'\n',{flag:'wx'});console.log(JSON.stringify(result));
 } catch(error) {
  if(!fs.existsSync(output))fs.writeFileSync(output,JSON.stringify({schema:'synthetic-timeline-capacity-v1',
   outcome:'failed',timestamp:new Date().toISOString(),htmlBytes:fs.statSync(html).size,
   browserVersion:browser.version(),error:error.message,pageErrors:errors},null,2)+'\n',{flag:'wx'});
  throw error;
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
