// Standalone development harness input. Not included in any player assembly.
using System;
using System.IO;
public static class IntakeWavInput
{
    public static void Main(string[] args)
    {
        if(args.Length!=3 || args[0]!="--development-audio")throw new ArgumentException("Explicit development audio flag required");
        using(var reader=new BinaryReader(File.OpenRead(args[1])))
        {
            if(new string(reader.ReadChars(4))!="RIFF")throw new Exception("WAV required");
            reader.ReadInt32();reader.ReadChars(4);
            short channels=0,bits=0;int rate=0;
            while(reader.BaseStream.Position<reader.BaseStream.Length)
            {
                string chunk=new string(reader.ReadChars(4));int size=reader.ReadInt32();
                if(chunk=="fmt ")
                {if(reader.ReadInt16()!=1)throw new Exception("PCM required");channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadBytes(6);bits=reader.ReadInt16();reader.ReadBytes(size-16);}
                else if(chunk=="data")
                {
                    if(channels!=1||bits!=16||rate!=24000)throw new Exception("24k mono PCM16 required");
                    var activity=new TrialReviewVoiceActivity(args[2]=="open");
                    var frame=new float[480];int frames=0;
                    while(size>=960 && activity.EndReason==null)
                    {for(int i=0;i<480;i++)frame[i]=reader.ReadInt16()/32768f;size-=960;activity.Feed(frame,1);frames+=480;}
                    Console.WriteLine("{\"reason\":\""+activity.EndReason+"\",\"samples\":"+frames+",\"speechDetected\":"+(activity.SpeechDetected?"true":"false")+"}");return;
                }
                else reader.ReadBytes(size);
                if(size%2!=0)reader.ReadByte();
            }
            throw new Exception("Missing PCM data");
        }
    }
}
