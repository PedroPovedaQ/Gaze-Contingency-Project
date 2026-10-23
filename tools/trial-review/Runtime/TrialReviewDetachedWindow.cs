using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public static class TrialReviewDetachedWindow
{
    // Copy only the complete prefix already parsed by the live viewer. Preserve raw
    // record text and available played WAVs; never freeze/copy a half-written tail.
    public static string Snapshot(TrialReplayTimeline timeline, string root)
    {
        string folder=Path.Combine(root,DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path=Path.Combine(folder,"recording.jsonl");
            using(var reader=new StreamReader(timeline.FilePath))
            using(var writer=new StreamWriter(path,false,new UTF8Encoding(false)))
                for(int i=0;i<timeline.Records.Count;i++)
                {
                    string line=reader.ReadLine();
                    if(line==null)throw new IOException("Recording changed while making the review snapshot.");
                    if(i==0 || i==timeline.Records.Count-1)
                    {
                        var record=JsonUtility.FromJson<TrialReplayRecord>(line);
                        if(record.sequence!=timeline.Records[i].sequence || i==0 && record.recordingId!=timeline.Header.recordingId)
                            throw new IOException("Recording identity changed while making the review snapshot.");
                    }
                    writer.WriteLine(line);
                }
            var copied=new HashSet<string>();
            foreach(var record in timeline.Records)
            {
                if(record.kind!="audio_playback_start" || string.IsNullOrEmpty(record.clip) || !copied.Add(record.clip))continue;
                string source=TrialReplayFile.AssetPath(timeline.DirectoryPath,record.clip);
                if(!File.Exists(source))continue; // The review will explicitly report missing audio.
                string destination=TrialReplayFile.AssetPath(folder,record.clip);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(source,destination);
            }
            File.WriteAllText(Path.Combine(folder,"review-snapshot.txt"),
                "Read-only review snapshot; source recording remains authoritative.\nSource: "+timeline.FilePath+
                "\nComplete records copied: "+timeline.Records.Count+"\nCaptured UTC: "+DateTime.UtcNow.ToString("O")+
                "\nOnly available played WAVs copied; raw-eye files and final exports remain in the original run.\n");
            return path;
        }
        catch { Directory.Delete(folder,true); throw; }
    }
}

public sealed partial class TrialReviewApp
{
    bool m_DetachedReview;
    void OpenReviewWindow()
    {
        try
        {
            if(Application.isEditor)throw new InvalidOperationException("Open a separate review window from the built Trial Review app.");
            string path=m_Timeline==null?null:TrialReviewDetachedWindow.Snapshot(m_Timeline,
                Path.Combine(m_LiveOutput,"review-snapshots"));
            string executable=Path.Combine(Application.dataPath,"MacOS","Trial Review");
            if(!File.Exists(executable))throw new FileNotFoundException("Trial Review executable unavailable",executable);
            string archive=Path.Combine(m_LiveOutput,"data","GazeReplays");
            string args="--review-window --no-auto-evaluation --browse-root "+Argument(archive)+
                " -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -logFile "+
                Argument(Path.Combine(Path.GetTempPath(),"trial-review-window-"+Guid.NewGuid().ToString("N")+".log"));
            if(path!=null)args+=" --recording "+Argument(path)+" --time "+m_Time.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            using(var child=Process.Start(new ProcessStartInfo(executable,args){UseShellExecute=false}))
                if(child==null)throw new IOException("Could not open the review window.");
        }
        catch(Exception e){m_Error="Review window: "+e.Message;}
    }
}
