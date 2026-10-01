// Exercise the actual raw replay converter and browser, without participant data.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { execFileSync } = require('node:child_process');
(async () => {
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'gaze-timeline-test-'));
 const analysis=path.resolve(__dirname,'..'),html=path.join(temp,'timeline.html');
 execFileSync('python3',['-c',
  'import sys,json;from pathlib import Path;sys.path.insert(0,sys.argv[1]);from test_replay_timeline import fixture;Path(sys.argv[2]).write_text("".join(json.dumps(r)+"\\n" for r in fixture()))',
  analysis,path.join(temp,'recording.jsonl')]);
 execFileSync('python3',[path.join(analysis,'replay_timeline.py'),path.join(temp,'recording.jsonl'),
  '--output',path.join(temp,'data.json'),'--html',html]);
 // Render-frame rows may share a timestamp. Sequence stepping must not apply
 // object changes from a later row merely because its timestamp is identical.
 const duplicate=path.join(temp,'duplicate.jsonl');
 const records=fs.readFileSync(path.join(temp,'recording.jsonl'),'utf8').trim().split('\n').map(JSON.parse);
 records[6].time=records[5].time;
 fs.writeFileSync(duplicate,records.map(r=>JSON.stringify(r)).join('\n')+'\n');
 const duplicateHTML=path.join(temp,'duplicate.html');
 execFileSync('python3',[path.join(analysis,'replay_timeline.py'),duplicate,
  '--output',path.join(temp,'duplicate.json'),'--html',duplicateHTML]);
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:1000}}),errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 const seek=seconds=>page.locator('#p20-time').fill(String(seconds));
 const rows=()=>page.locator('#p20-data').innerText();
 const raw=async()=>JSON.parse(await page.locator('#p20-raw').textContent());
 const target=()=>page.locator('#p20-map circle[data-tooltip^="Blue Cube"]');
 try {
  await page.goto(pathToFileURL(html).href);
  assert.match(await rows(),/Not yet available/);
  await seek(1.01);
  assert.match(await rows(),/Searching/);
  assert.match(await rows(),/sequence 5 · line 6/);
  assert.equal((await raw()).sample.gazeRecordingId,'raw-a');
  assert.match(await target().getAttribute('data-tooltip'),/Y 1.00 m/);
  await page.locator('#p20-next-sample').click();
  assert.equal((await raw()).sample.sequence,6);
  assert.match(await target().getAttribute('data-tooltip'),/Y 2.00 m/);
  await target().click();
  assert.equal(JSON.parse(await page.locator('#p20-object').textContent()).position.y,2);
  await page.locator('#p20-previous-sample').click();
  assert.equal((await raw()).sample.sequence,5);
  assert.match(await target().getAttribute('data-tooltip'),/Y 1.00 m/);
  assert.equal(JSON.parse(await page.locator('#p20-object').textContent()).position.y,1);
  // A gap during pause never displays the previous measured pose as current.
  await seek(1.5);
  assert.match(await rows(),/Paused \/ transition · no current pose/);
  assert.match(await rows(),/Head XYZ\s+Unavailable/);
  assert.equal(await target().count(),0);
  assert.match((await raw()).sampleStatus,/Historical/);
  const downloadPromise=page.waitForEvent('download');await page.locator('#p20-export').click();
  const download=await downloadPromise,out=path.join(temp,'cursor.json');await download.saveAs(out);
  const snapshot=JSON.parse(fs.readFileSync(out));
  assert.equal(snapshot.recordingSeconds,2.5);assert.equal(snapshot.poseCurrent,false);
  assert.equal(snapshot.sample.sequence,6);assert.equal(snapshot.sampleSourceLine,7);
  assert.equal(snapshot.objects[0][3],2);assert.equal(snapshot.source.sha256.length,64);
  assert.equal(snapshot.finalTrialSummary,null);
  assert.equal(snapshot.selectedObject.position.y,2);
  await page.locator('#p20-event').selectOption({label:'1.05 s · search_paused · sequence 7'});
  assert.match(await rows(),/Paused \/ transition/);
  await seek(2.01);
  assert.match(await rows(),/Completed/);
  assert.match(await page.locator('#p20-choice').innerText(),/Correct selection/);
  // Rewind reconstitutes the prior layout, visibility and uncompleted state.
  await seek(1.01);assert.match(await rows(),/Searching/);
  assert.equal(await page.locator('#p20-choice').innerText(),'No selection yet.');
  assert.match(await target().getAttribute('data-tooltip'),/Y 1.00 m/);
  await page.locator('summary').filter({hasText:'Exact recorded'}).click();
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  if(process.env.TIMELINE_TEST_SCREENSHOTS){fs.mkdirSync(process.env.TIMELINE_TEST_SCREENSHOTS,{recursive:true});await page.screenshot({path:path.join(process.env.TIMELINE_TEST_SCREENSHOTS,'raw-replay-mobile.png'),fullPage:true});}
  await page.goto(pathToFileURL(duplicateHTML).href);await seek(1.01);
  assert.equal((await raw()).sample.sequence,6);
  await page.locator('#p20-previous-sample').click();
  assert.equal((await raw()).sample.sequence,5);
  assert.match(await target().getAttribute('data-tooltip'),/Y 1.00 m/);
  await page.locator('#p20-next-sample').click();
  assert.equal((await raw()).sample.sequence,6);
  assert.match(await target().getAttribute('data-tooltip'),/Y 2.00 m/);
  assert.deepEqual(errors,[]);
  console.log('PASS: direct JSONL import, exact source samples, sample stepping, object movement, pause gaps, cursor export, backward reconstruction, mobile layout.');
 } finally {await browser.close();fs.rmSync(temp,{recursive:true,force:true});}
})().catch(error=>{console.error(error);process.exitCode=1;});
