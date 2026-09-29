 // Video and telemetry use the continuous recording clock, not searchSeconds.
 // Positive offset means the camera started after replay recording began.
 const video=el('video'), runKey=JSON.stringify([data.participant,data.utc]);
 let videoFile=null, videoURL=null, offset=null, videoFailed=false, playPending=false, videoGeneration=0;
 const setVideoStatus=message=>{el('video-status').textContent=message;};
 const fileIdentity=file=>({name:file.name,size:file.size,lastModified:file.lastModified});
 const sameFile=(a,b)=>a && b && a.name===b.name && a.size===b.size && a.lastModified===b.lastModified;
 const videoReady=()=>videoFile && !videoFailed && video.readyState>=1 && Number.isFinite(video.duration) && video.duration>0;
 const covered=()=>offset!==null && tri().start+time-offset>=0 && tri().start+time-offset<video.duration;
 function updateVideoButtons(){
  const ready=videoReady();
  el('video-align').disabled=!ready || offset!==null;
  el('video-adjust').disabled=!ready || offset===null;
  el('video-apply').disabled=!ready;
  el('video-save').disabled=!ready || offset===null;
  el('video-remove').disabled=!videoFile;
 }
 function removeVideo(resetPicker=true){
  stop();videoGeneration++;playPending=false;video.pause();video.removeAttribute('src');video.load();
  if(videoURL)URL.revokeObjectURL(videoURL);
  videoURL=null;videoFile=null;offset=null;videoFailed=false;video.hidden=true;
  if(resetPicker)el('video-file').value='';el('video-offset').value='';
  el('video-alignment').textContent='Select a video before aligning. Reattach the video after reopening this page.';
  setVideoStatus('No video attached. Telemetry playback is available.');updateVideoButtons();
 }
 function setOffset(value){
  if(!videoReady() || !Number.isFinite(value)){setVideoStatus('Enter a finite recording-time offset.');return;}
  stop();offset=value;video.controls=false;
  el('video-offset').value=value.toFixed(3);
  el('video-alignment').textContent=`Manual alignment: video 0.000 s = recording ${value.toFixed(3)} s. Approximate; verify a later event for drift.`;
  updateVideoButtons();syncVideo(true);
 }
 function syncVideo(force=false){
  if(!videoReady() || offset===null)return;
  const target=tri().start+time-offset;
  if(!covered()){
   video.pause();video.hidden=true;
   setVideoStatus(target<0?'No video coverage at this time · recording starts later.':'No video coverage at this time · recording has ended.');
   return;
  }
  video.playbackRate=Number(el('speed').value);
  if((force || Math.abs(video.currentTime-target)>(playing?.18:.001)) && !video.seeking){
   video.currentTime=target;
  }
  // Hide stale frames while a seek settles, including backwards trial changes.
  video.hidden=video.seeking;
  setVideoStatus(video.seeking?'Seeking video…':playing && video.readyState<3?'Buffering video · timeline waiting…':`${videoFile.name} · ${fmt(target)} s video · manual alignment`);
  if(playing && video.paused && !playPending){
   playPending=true;const generation=videoGeneration;
   video.play().catch(error=>{
    if(generation!==videoGeneration || !playing)return;
    stop();setVideoStatus('Video playback stopped: '+error.message+'. Press Play to retry.');
   }).finally(()=>{if(generation===videoGeneration)playPending=false;});
  }else if(!playing){video.pause();}
 }
 el('video-file').addEventListener('change',()=>{
  const selected=el('video-file').files[0];if(!selected)return;
  removeVideo(false);videoFile=selected;videoURL=URL.createObjectURL(selected);
  video.controls=true;video.src=videoURL;video.hidden=false;
  setVideoStatus('Loading video…');updateVideoButtons();
 });
 video.addEventListener('loadedmetadata',()=>{
  if(!videoFile)return;
  if(!Number.isFinite(video.duration) || video.duration<=0){
   videoFailed=true;setVideoStatus('Video duration is unavailable. Choose a finalized recording.');
  }else{setVideoStatus('Video loaded · not aligned. Pause at the same event in both views, then align.');}
  updateVideoButtons();
 });
 video.addEventListener('error',()=>{
  if(!videoFile)return;
  stop();videoFailed=true;video.hidden=true;
  setVideoStatus('Cannot read this video. Choose a browser-playable MP4 or WebM, or remove it to use telemetry only.');
  updateVideoButtons();
 });
 video.addEventListener('seeked',()=>{if(offset!==null)syncVideo();});
 // Native video controls are available only while manually choosing the anchor.
 // Starting that preview cannot run a second independent timeline clock.
 video.addEventListener('play',()=>{if(offset===null && playing){playing=false;play.textContent='Play';cancelAnimationFrame(animation);}});
 el('video-align').addEventListener('click',()=>setOffset(tri().start+time-video.currentTime));
 el('video-apply').addEventListener('click',()=>{
  if(el('video-offset').value.trim()===''){setVideoStatus('Enter the recording second corresponding to video start.');return;}
  setOffset(Number(el('video-offset').value));
 });
 el('video-adjust').addEventListener('click',()=>{
  stop();offset=null;video.controls=true;video.hidden=false;
  el('video-alignment').textContent='Adjusting alignment. Pause both views at the same event, then align again.';
  setVideoStatus('Preview only · video is not synchronized while adjusting.');updateVideoButtons();
 });
 el('video-remove').addEventListener('click',()=>removeVideo());
 el('video-audio').addEventListener('change',()=>{video.muted=!el('video-audio').checked;});
 el('speed').addEventListener('change',()=>syncVideo(true));
 el('video-save').addEventListener('click',()=>{
  if(!videoReady() || offset===null)return;
  const record={schema:'run-video-alignment-v1',runKey,file:fileIdentity(videoFile),
   recordingSecondsAtVideoStart:offset,method:'manual_constant_offset',savedAt:new Date().toISOString()};
  const url=URL.createObjectURL(new Blob([JSON.stringify(record,null,2)],{type:'application/json'}));
  const a=document.createElement('a');a.href=url;a.download='run-video-alignment.json';a.click();
  setTimeout(()=>URL.revokeObjectURL(url),1000);
 });
 el('video-restore').addEventListener('change',async()=>{
  const selected=el('video-restore').files[0];el('video-restore').value='';if(!selected)return;
  const generation=videoGeneration;
  try{
   if(selected.size>65536)throw new Error('Alignment file is too large.');
   const record=JSON.parse(await selected.text());
   if(generation!==videoGeneration)return;
   if(record.schema!=='run-video-alignment-v1' || record.runKey!==runKey)throw new Error('Alignment belongs to a different run.');
   if(!videoFile || !sameFile(record.file,fileIdentity(videoFile)))throw new Error('Attach the same video file before restoring its alignment.');
   if(record.method!=='manual_constant_offset' || typeof record.recordingSecondsAtVideoStart!=='number' || !Number.isFinite(record.recordingSecondsAtVideoStart))throw new Error('Invalid video offset.');
   setOffset(record.recordingSecondsAtVideoStart);
  }catch(error){setVideoStatus(error.message);}
 });
 window.addEventListener('pagehide',event=>{stop();if(!event.persisted && videoURL)URL.revokeObjectURL(videoURL);});
