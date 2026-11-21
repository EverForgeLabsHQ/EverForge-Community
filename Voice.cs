using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Threading;

namespace ActionJackson {
 internal sealed class Microphone {
  public int Id;public string Name;
  public override string ToString(){return Name;}
 }
 internal sealed class WaveInput : Stream {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Caps {public ushort mid,pid;public uint version;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string name;public uint formats;public ushort channels,reserved;}
  [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {public ushort tag,channels;public uint rate,bytesPerSec;public ushort align,bits,extra;}
  [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr data;public uint length,recorded;public UIntPtr user;public uint flags,loops;public IntPtr next;public UIntPtr reserved;}
  delegate void Callback(IntPtr h,uint message,UIntPtr instance,IntPtr p1,IntPtr p2);
  [DllImport("winmm.dll")] static extern uint waveInGetNumDevs();
  [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint waveInGetDevCaps(UIntPtr id,out Caps caps,uint size);
  [DllImport("winmm.dll")] static extern uint waveInOpen(out IntPtr h,uint id,ref Format f,Callback callback,UIntPtr instance,uint flags);
  [DllImport("winmm.dll")] static extern uint waveInPrepareHeader(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveInUnprepareHeader(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveInAddBuffer(IntPtr h,IntPtr header,uint size);
  [DllImport("winmm.dll")] static extern uint waveInStart(IntPtr h);
  [DllImport("winmm.dll")] static extern uint waveInStop(IntPtr h);
  [DllImport("winmm.dll")] static extern uint waveInReset(IntPtr h);
  [DllImport("winmm.dll")] static extern uint waveInClose(IntPtr h);
  readonly BlockingCollection<byte[]> queue=new BlockingCollection<byte[]>(40);
  readonly List<IntPtr> buffers=new List<IntPtr>(),headers=new List<IntPtr>();
  Callback callback;IntPtr handle;volatile bool closed;byte[] current;int position;long totalRead,totalReceived;int lastPeak;
  public string Diagnostic{get{return "Mic "+(Interlocked.Read(ref totalReceived)/32000)+"s  ·  processed "+(Interlocked.Read(ref totalRead)/32000)+"s  ·  level "+lastPeak+"%";}}
  public event Action<int> Level;
  public static List<Microphone> Devices(){
   var devices=new List<Microphone>{new Microphone{Id=-1,Name="Default microphone"}};
   for(uint i=0;i<waveInGetNumDevs();i++){Caps c;if(waveInGetDevCaps(new UIntPtr(i),out c,(uint)Marshal.SizeOf(typeof(Caps)))==0)devices.Add(new Microphone{Id=(int)i,Name=c.name});}
   return devices;
  }
  public WaveInput(int device){
   callback=OnData;
   var f=new Format{tag=1,channels=1,rate=16000,bytesPerSec=32000,align=2,bits=16};
   uint error=waveInOpen(out handle,unchecked((uint)device),ref f,callback,UIntPtr.Zero,0x30000);
   if(error!=0)throw new InvalidOperationException("Cannot open this microphone (Windows audio error "+error+"). Choose another microphone.");
   try {
    for(int i=0;i<4;i++){
     IntPtr data=Marshal.AllocHGlobal(4096),header=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Header)));
     buffers.Add(data);headers.Add(header);
     Marshal.StructureToPtr(new Header{data=data,length=4096},header,false);
     Check(waveInPrepareHeader(handle,header,(uint)Marshal.SizeOf(typeof(Header))));
     Check(waveInAddBuffer(handle,header,(uint)Marshal.SizeOf(typeof(Header))));
    }
    Check(waveInStart(handle));
   }catch{Dispose();throw;}
  }
  static void Check(uint result){if(result!=0)throw new InvalidOperationException("Microphone audio error "+result+".");}
  void OnData(IntPtr h,uint message,UIntPtr instance,IntPtr p1,IntPtr p2){
   if(message!=0x3C0||closed)return;
   try{
    var header=(Header)Marshal.PtrToStructure(p1,typeof(Header));
    if(header.recorded>0){
     var data=new byte[header.recorded];Marshal.Copy(header.data,data,0,data.Length);
     Interlocked.Add(ref totalReceived,data.Length);
     queue.TryAdd(data);
     int peak=0;for(int i=0;i+1<data.Length;i+=2){int value=Math.Abs((int)(short)(data[i]|(data[i+1]<<8)));if(value>peak)peak=value;}
     lastPeak=Math.Min(100,peak*100/15000);var listener=Level;if(listener!=null)listener(lastPeak);
    }
    if(!closed)waveInAddBuffer(h,p1,(uint)Marshal.SizeOf(typeof(Header)));
   }catch(InvalidOperationException){}
  }
  public override int Read(byte[] buffer,int offset,int count){
   if(count==0)return 0;
   int filled=0;
   while(filled<count){
    while(current==null||position>=current.Length){
     if(closed)return filled;
     if(!queue.TryTake(out current,200)){if(queue.IsCompleted)return filled;continue;}position=0;
    }
    int take=Math.Min(count-filled,current.Length-position);Buffer.BlockCopy(current,position,buffer,offset+filled,take);position+=take;totalRead+=take;filled+=take;
   }
   return filled;
  }
  protected override void Dispose(bool disposing){
   if(closed)return;closed=true;queue.CompleteAdding();
   if(handle!=IntPtr.Zero){waveInStop(handle);waveInReset(handle);foreach(var h in headers)waveInUnprepareHeader(handle,h,(uint)Marshal.SizeOf(typeof(Header)));waveInClose(handle);handle=IntPtr.Zero;}
   foreach(var p in headers)Marshal.FreeHGlobal(p);foreach(var p in buffers)Marshal.FreeHGlobal(p);headers.Clear();buffers.Clear();base.Dispose(disposing);
  }
  public override bool CanRead{get{return !closed;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
  // SAPI queries length/position even for a live, non-seekable audio stream.
  public override long Length{get{return long.MaxValue;}}public override long Position{get{return totalRead;}set{if(value!=totalRead)throw new NotSupportedException();}}
  public override void Flush(){}public override long Seek(long x,SeekOrigin o){if((o==SeekOrigin.Current&&x==0)||(o==SeekOrigin.Begin&&x==totalRead))return totalRead;throw new NotSupportedException();}public override void SetLength(long n){throw new NotSupportedException();}public override void Write(byte[] b,int o,int c){throw new NotSupportedException();}
 }
 internal sealed class Voice : IDisposable {
  SpeechRecognitionEngine engine;WaveInput input;OfflineVoice offline;volatile bool disposed;
  public string Diagnostic{get{return offline!=null?offline.Diagnostic:input==null?"Microphone off":input.Diagnostic;}}
  public event Action<string,float> Heard;public event Action<int> Level;public event Action<string> Fault;public event Action<string> Feedback;
  internal static SpeechRecognitionEngine CreateEngine(){
   var available=SpeechRecognitionEngine.InstalledRecognizers();
   var choice=available.FirstOrDefault(r=>r.Culture.Name=="en-US")??available.FirstOrDefault(r=>r.Culture.TwoLetterISOLanguageName=="en");
   if(choice==null)throw new InvalidOperationException("An English Windows speech recognizer is required.");
   var engine=new SpeechRecognitionEngine(choice);
   var grammar=new GrammarBuilder{Culture=choice.Culture};
   grammar.Append(new Choices("call","check","fold","Jackson call","Jackson check","Jackson fold","stop listening","Jackson stop"));
   engine.LoadGrammar(new Grammar(grammar));
   engine.InitialSilenceTimeout=TimeSpan.FromSeconds(0);
   engine.BabbleTimeout=TimeSpan.FromSeconds(0);
   engine.EndSilenceTimeout=TimeSpan.FromMilliseconds(220);
   engine.EndSilenceTimeoutAmbiguous=TimeSpan.FromMilliseconds(320);
   return engine;
  }
  public Voice(int device){
   try{
    if(Directory.Exists(LocalRecognizer.ModelPath)){
     offline=new OfflineVoice(device);
     offline.Heard+=(text,confidence)=>{var h=Heard;if(h!=null&&!disposed)h(text,confidence);};
     offline.Level+=level=>{var h=Level;if(h!=null&&!disposed)h(level);};
     offline.Feedback+=message=>{var h=Feedback;if(h!=null&&!disposed)h(message);};
     offline.Fault+=message=>{var h=Fault;if(h!=null&&!disposed)h(message);};
     return;
    }
    engine=CreateEngine();input=new WaveInput(device);
    input.Level+=v=>{var h=Level;if(h!=null&&!disposed)h(v);};
    engine.SetInputToAudioStream(input,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));
    engine.SpeechRecognized+=(s,e)=>{var h=Heard;if(h!=null&&!disposed)h(e.Result.Text,e.Result.Confidence);};
    engine.SpeechRecognitionRejected+=(s,e)=>{var h=Feedback;if(h!=null&&!disposed)h("Unclear speech — please repeat.");};
    engine.SpeechDetected+=(s,e)=>{var h=Feedback;if(h!=null&&!disposed)h("Hearing your voice…");};
    engine.AudioSignalProblemOccurred+=(s,e)=>{var h=Feedback;if(h!=null&&!disposed)h("Mic signal: "+e.AudioSignalProblem);};
    engine.RecognizeCompleted+=(s,e)=>{if(!disposed){var h=Fault;if(h!=null)h(e.Error==null?"Microphone stopped (stream="+e.InputStreamEnded+", silence="+e.InitialSilenceTimeout+", babble="+e.BabbleTimeout+"). Press Start.":e.Error.Message);}};
   }catch{Dispose();throw;}
  }
  public void Start(){if(offline!=null)offline.Start();else engine.RecognizeAsync(RecognizeMode.Multiple);}
  public void Dispose(){
   if(disposed)return;disposed=true;
   if(offline!=null){offline.Dispose();offline=null;}
   // End the blocking stream before disposing the recognizer.
   if(input!=null)input.Dispose();
   if(engine!=null){try{engine.RecognizeAsyncCancel();}catch{}engine.Dispose();engine=null;}
  }
 }
}
