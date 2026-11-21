using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ActionJackson {
 internal static class VoskNative {
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vosk_set_log_level(int level);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vosk_model_new([MarshalAs(UnmanagedType.LPStr)] string path);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vosk_model_free(IntPtr model);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_new_grm(IntPtr model,float rate,[MarshalAs(UnmanagedType.LPStr)] string grammar);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vosk_recognizer_set_words(IntPtr recognizer,int words);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int vosk_recognizer_accept_waveform(IntPtr recognizer,byte[] data,int length);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_final_result(IntPtr recognizer);
  [DllImport("libvosk.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vosk_recognizer_free(IntPtr recognizer);
 }
 internal sealed class LocalRecognizer : IDisposable {
  IntPtr model,recognizer;
  internal static string ModelPath {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vosk-model-small-en-us-0.15");}}
  internal LocalRecognizer(){
   VoskNative.vosk_set_log_level(-1);model=VoskNative.vosk_model_new(ModelPath);
   if(model==IntPtr.Zero)throw new InvalidOperationException("Cannot load the local English voice model. Keep the model folder beside Action Jackson.exe.");
   recognizer=VoskNative.vosk_recognizer_new_grm(model,16000,"[\"call\",\"check\",\"fold\",\"jackson call\",\"jackson check\",\"jackson fold\",\"stop listening\",\"jackson stop\",\"[unk]\"]");
   if(recognizer==IntPtr.Zero){Dispose();throw new InvalidOperationException("Cannot start local speech recognition.");}
   VoskNative.vosk_recognizer_set_words(recognizer,1);
  }
  internal bool Feed(byte[] bytes,int count){return VoskNative.vosk_recognizer_accept_waveform(recognizer,bytes,count)!=0;}
  internal string Result(bool final){return Marshal.PtrToStringAnsi(final?VoskNative.vosk_recognizer_final_result(recognizer):VoskNative.vosk_recognizer_result(recognizer));}
  internal string Partial(){return Text(Marshal.PtrToStringAnsi(VoskNative.vosk_recognizer_partial_result(recognizer)),"partial");}
  internal static string Text(string json,string key){return Regex.Match(json??"","\""+key+"\"\\s*:\\s*\"([^\"]*)\"").Groups[1].Value.Trim();}
  internal static float Confidence(string json){var values=Regex.Matches(json??"","\"conf\"\\s*:\\s*([0-9.]+)").Cast<Match>().Select(m=>float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();return values.Length>0?values.Min():0;}
  public void Dispose(){if(recognizer!=IntPtr.Zero){VoskNative.vosk_recognizer_free(recognizer);recognizer=IntPtr.Zero;}if(model!=IntPtr.Zero){VoskNative.vosk_model_free(model);model=IntPtr.Zero;}}
 }
 internal sealed class OfflineVoice : IDisposable {
  LocalRecognizer recognizer;WaveInput input;Task worker;volatile bool stopped;
  public event Action<string,float> Heard;public event Action<int> Level;public event Action<string> Fault;public event Action<string> Feedback;
  public string Diagnostic{get{return input==null?"Microphone off":input.Diagnostic;}}
  public OfflineVoice(int device){try{recognizer=new LocalRecognizer();input=new WaveInput(device);input.Level+=v=>{var h=Level;if(h!=null&&!stopped)h(v);};}catch{Dispose();throw;}}
  public void Start(){worker=Task.Factory.StartNew(Run,TaskCreationOptions.LongRunning);}
  void Run(){
   try{
    var buffer=new byte[4096];string previous="";
    while(!stopped){
     int count=input.Read(buffer,0,buffer.Length);if(count==0)break;
     if(recognizer.Feed(buffer,count)){
      var result=recognizer.Result(false);var text=LocalRecognizer.Text(result,"text");previous="";
      if(text.Length==0)continue;
      if(text.Contains("[unk]")){var feedback=Feedback;if(feedback!=null&&!stopped)feedback("Unclear speech — no click.");continue;}
      var heard=Heard;if(heard!=null&&!stopped)heard(text,LocalRecognizer.Confidence(result));
     }else{
      var text=recognizer.Partial();if(text!=previous&&text.Length>0&&!text.Contains("[unk]")){previous=text;var feedback=Feedback;if(feedback!=null&&!stopped)feedback("Hearing: "+text+"…");}
     }
    }
    if(!stopped){var fault=Fault;if(fault!=null)fault("Microphone disconnected. Press Start to reconnect.");}
   }catch(Exception ex){if(!stopped){var fault=Fault;if(fault!=null)fault(ex.Message);}}
   finally{if(recognizer!=null){recognizer.Dispose();recognizer=null;}}
  }
  public void Dispose(){if(stopped)return;stopped=true;if(input!=null)input.Dispose();if(worker!=null){try{worker.Wait(3000);}catch{}}else if(recognizer!=null){recognizer.Dispose();recognizer=null;}}
 }
}
