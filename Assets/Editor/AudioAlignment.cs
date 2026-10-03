using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace SealGugu.Editor
{
    public static class AudioAlignment
    {
        public static void DumpImportedAudio()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-guguAudioDump");
            if(index<0||index+1>=args.Length)throw new ArgumentException("-guguAudioDump output-directory required");
            string output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
            var charts=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text);
            foreach(var track in charts.tracks){
                string path=track.src.Replace("assets/","Audio/");path=path.Substring(0,path.LastIndexOf('.'));
                var clip=Resources.Load<AudioClip>(path);clip.LoadAudioData();
                var samples=new float[Math.Min(clip.samples,clip.frequency*10)*clip.channels];
                if(!clip.GetData(samples,0))throw new Exception("Cannot read "+path);
                using(var writer=new BinaryWriter(File.Create(Path.Combine(output,track.id+".f32"))))foreach(float sample in samples)writer.Write(sample);
                File.WriteAllText(Path.Combine(output,track.id+".json"),"{\"sampleRate\":"+clip.frequency+",\"channels\":"+clip.channels+",\"samples\":"+clip.samples+"}");
                Debug.Log("GUGU_AUDIO_DUMP "+track.id+" "+clip.samples+" samples");
            }
        }
    }
}
