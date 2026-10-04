using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace SealGugu {
    /// <summary>Read only the four old game preferences; retain current values if already set.</summary>
    public static class HuhuSettingsMigration {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegOpenKeyExW(IntPtr key,string subkey,uint options,int access,out IntPtr result);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegEnumValueW(IntPtr key,uint index,StringBuilder name,ref uint nameLength,IntPtr reserved,out uint type,byte[] data,ref uint dataLength);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
#endif
        public static void Once(){
            const string marker="huhu.imported-gugu-settings";
            if(PlayerPrefs.GetInt(marker,0)==1)return;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr previous=IntPtr.Zero;
            try {
                string path=(Application.isEditor?@"Software\Unity\UnityEditor\":@"Software\")+@"第二組\海豹咕咕";
                if(RegOpenKeyExW(new IntPtr(unchecked((int)0x80000001)),path,0,0x20019,out previous)==0){
                    for(uint index=0;index<1024;index++){
                        var name=new StringBuilder(512);uint length=512,dataLength=16;var bytes=new byte[16];
                        int result=RegEnumValueW(previous,index,name,ref length,IntPtr.Zero,out uint type,bytes,ref dataLength);
                        if(result==259)break;if(result!=0||dataLength!=4||(type!=3&&type!=4))continue;
                        foreach(string key in new[]{"gugu.speed","gugu.delay","gugu.window","gugu.reduced"}){
                            if(PlayerPrefs.HasKey(key)||(name.ToString()!=key&&!name.ToString().StartsWith(key+"_h",StringComparison.Ordinal)))continue;
                            if(key=="gugu.reduced"){PlayerPrefs.SetInt(key,BitConverter.ToInt32(bytes,0)==1?1:0);continue;}
                            float number=BitConverter.ToSingle(bytes,0);if(float.IsNaN(number)||float.IsInfinity(number))continue;
                            if(key=="gugu.speed")number=(float)GuguRun.speedValue(number);
                            else if(key=="gugu.delay")number=(float)GuguRun.delayValue(number);
                            else number=(float)GuguRun.windowValue(number);
                            PlayerPrefs.SetFloat(key,number);
                        }
                    }
                }
            }catch(Exception){Debug.LogWarning("Previous game calibration could not be read; current settings remain available.");}
            finally{if(previous!=IntPtr.Zero)RegCloseKey(previous);}
#endif
            PlayerPrefs.SetInt(marker,1);PlayerPrefs.Save();
        }
    }
}
