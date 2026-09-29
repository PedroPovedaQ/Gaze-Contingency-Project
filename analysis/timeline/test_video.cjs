// Real-browser integration test; run with Playwright available on NODE_PATH.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { execFileSync } = require('node:child_process');
(async () => {
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'gaze-video-test-'));
 const clip=path.join(temp,'synthetic.mp4'), html=path.join(temp,'timeline.html');
 const trial=(id,start,end)=>({id,sourceId:id,label:id,targetName:'Blue Cube',target:'target',theta:45,
  start,end,snapshotTime:start,searchStart:start+1,completeTime:null,duration:null,practice:false,
  objects:[['target','Blue Cube',0,1,2,0]],samples:[],speech:[],events:[{time:start+1,kind:'search_start'}],
  gaps:[],sampleCount:0,medianHz:0});
 fs.writeFileSync(path.join(temp,'data.json'),JSON.stringify({participant:'SYNTHETIC',utc:'test-recording',complete:false,
  trials:[trial('Trial 1',10,14),trial('Trial 2',14,20)]}));
 execFileSync('python3',[path.join(__dirname,'../timeline_page.py'),path.join(temp,'data.json'),'--output',html]);
 execFileSync('ffmpeg',['-loglevel','error','-f','lavfi','-i','testsrc2=size=640x360:rate=30','-t','4','-c:v','libx264','-pix_fmt','yuv420p',clip]);
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:1000}});
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 const video=()=>page.locator('#p20-video');
 const status=()=>page.locator('#p20-video-status').innerText();
 const seek=async seconds=>{await page.locator('#p20-time').fill(String(seconds));};
 const near=async expected=>{await page.waitForFunction(expected=>Math.abs(document.querySelector('video').currentTime-expected)<.08,expected);};
 try {
  await page.goto(pathToFileURL(html).href);
  assert.match(await status(),/No video attached/);
  await page.locator('#p20-video-file').setInputFiles(clip);
  await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
  assert.match(await status(),/not aligned/);
  await page.locator('#p20-play').click();
  assert.match(await status(),/Align the video/);
  // Camera starts at recording second 11, inside trial 1 (not its search clock).
  await page.locator('summary').filter({hasText:'Video alignment'}).click();
  await page.locator('#p20-video-offset').fill('11');
  await page.locator('#p20-video-apply').click();
  assert.match(await status(),/starts later/);
  assert.equal(await video().isVisible(),false);
  await seek(2);await near(1);
  await page.waitForFunction(()=>!document.querySelector('video').hidden);
  await page.locator('#p20-speed').selectOption('2');
  await page.locator('#p20-play').click();
  await page.waitForFunction(()=>document.querySelector('video').currentTime>1.25);
  await page.locator('#p20-play').click();
  assert.equal(await video().evaluate(v=>v.paused),true);
  assert.equal(await video().evaluate(v=>v.playbackRate),2);
  // Changing trial keeps recording-time mapping instead of restarting the clip.
  await page.locator('#p20-trial').selectOption('1');await near(3);
  await seek(.8);await page.locator('#p20-play').click();
  await page.waitForFunction(()=>Number(document.querySelector('#p20-time').value)>1.3);
  await page.locator('#p20-play').click();
  assert.match(await status(),/has ended/);
  await seek(2);assert.match(await status(),/has ended/);
  assert.equal(await video().isVisible(),false);
  await page.locator('#p20-trial').selectOption('0');
  await page.locator('#p20-next').click();await near(0);
  await seek(1.5);await near(.5);
  const downloadPromise=page.waitForEvent('download');await page.locator('#p20-video-save').click();
  const download=await downloadPromise;const config=path.join(temp,'alignment.json');await download.saveAs(config);
  const saved=JSON.parse(fs.readFileSync(config));assert.equal(saved.recordingSecondsAtVideoStart,11);
  await page.locator('#p20-video-adjust').click();
  await video().evaluate(v=>{v.currentTime=1;});await near(1);
  await page.locator('#p20-video-align').click();
  assert.equal(await page.locator('#p20-video-offset').inputValue(),'10.500');
  await page.locator('#p20-video-restore').setInputFiles(config);
  await page.waitForFunction(()=>document.querySelector('#p20-video-offset').value==='11.000');
  const bad=path.join(temp,'wrong-run.json');fs.writeFileSync(bad,JSON.stringify({...saved,runKey:'other'}));
  await page.locator('#p20-video-restore').setInputFiles(bad);
  await page.waitForFunction(()=>document.querySelector('#p20-video-status').textContent.includes('different run'));
  assert.equal(await page.locator('#p20-video-offset').inputValue(),'11.000');
  await page.locator('#p20-video-remove').click();assert.match(await status(),/No video attached/);
  await page.locator('#p20-play').click();
  await page.waitForFunction(()=>Number(document.querySelector('#p20-time').value)>1.7);
  await page.locator('#p20-play').click();
  // Invalid media must not display stale video or silently retain its alignment.
  const invalid=path.join(temp,'broken.mp4');fs.writeFileSync(invalid,'not a video');
  await page.locator('#p20-video-file').setInputFiles(invalid);
  await page.waitForFunction(()=>document.querySelector('#p20-video-status').textContent.includes('Cannot read'));
  assert.equal(await video().isVisible(),false);
  await page.locator('#p20-video-file').setInputFiles(clip);
  await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
  assert.match(await status(),/not aligned/);
  await page.locator('#p20-video-restore').setInputFiles(config);
  await page.waitForFunction(()=>document.querySelector('#p20-video-offset').value==='11.000');
  await seek(2);await near(1);
  const output=process.env.TIMELINE_TEST_SCREENSHOTS;
  if(output){fs.mkdirSync(output,{recursive:true});await page.screenshot({path:path.join(output,'desktop.png'),fullPage:true});}
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  if(output)await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
  assert.deepEqual(errors,[]);
  console.log('PASS: video load, alignment, seek, speed, pause, trial switching, coverage gaps, saved mapping, run mismatch, invalid media, responsive layout.');
 } finally {await browser.close();fs.rmSync(temp,{recursive:true,force:true});}
})().catch(error=>{console.error(error);process.exitCode=1;});
