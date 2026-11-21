using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ActionJackson {
 internal static class Program {
  internal static string DirectoryPath {get{return AppDomain.CurrentDomain.BaseDirectory;}}
  [STAThread] static void Main(string[] args){
   try{Run(args);}catch(Exception ex){File.WriteAllText(Path.Combine(DirectoryPath,"startup-error.txt"),ex.ToString());Environment.ExitCode=1;}
  }
  static void Run(string[] args){
   Native.SetDpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Contains("--self-test")){Tests.Run(args).GetAwaiter().GetResult();return;}
   if(args.Contains("--integration-test")){Application.Run(new IntegrationForm());return;}
   if(args.Contains("--practice")){Application.Run(new PracticeForm());return;}
   bool first;using(var mutex=new Mutex(true,"Local\\ActionJacksonDesktopVoice",out first)){
    if(!first){foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath))){try{if(p.Id!=Process.GetCurrentProcess().Id&&p.MainWindowTitle=="Action Jackson"){Native.ShowWindow(p.MainWindowHandle,9);Native.SetForegroundWindow(p.MainWindowHandle);return;}}catch{}}return;}
    Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"Action Jackson");
    Application.Run(new MainForm(args.Contains("--voice-test")));
   }
  }
  internal static Icon LoadIcon(){try{return new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("soldier.ico"));}catch{return SystemIcons.Application;}}
  internal static Image LoadSoldier(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("soldier.png"))return new Bitmap(s);}
 }
 internal class MainForm : Form {
  readonly Color ink=Color.FromArgb(235,240,227),muted=Color.FromArgb(157,173,148),green=Color.FromArgb(155,216,96),panel=Color.FromArgb(31,42,33);
  Label status,detail,lastHeard,scanInfo;
  Button power;Button[] actionButtons;ComboBox microphone;CheckBox test;ProgressBar meter;
  System.Windows.Forms.Timer timer;Detector detector;Voice voice;Scan current;CancellationTokenSource stop;
  bool active,busy,commandBusy,closing;int generation,scanCount;DateTime lastCommand=DateTime.MinValue;
  string lastResult="Say call, check, or fold.";
  public MainForm(bool voiceTest){
   Text="Action Jackson";Icon=Program.LoadIcon();ClientSize=new Size(370,431);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   BackColor=Color.FromArgb(19,27,21);ForeColor=ink;Font=new Font("Segoe UI",9F);AutoScaleMode=AutoScaleMode.Dpi;TopMost=true;
   StartPosition=FormStartPosition.Manual;var work=Screen.PrimaryScreen.WorkingArea;Location=new Point(work.Right-Width-28,work.Top+45);
   var portrait=new PictureBox{Location=new Point(15,11),Size=new Size(82,88),SizeMode=PictureBoxSizeMode.Zoom,Image=Program.LoadSoldier()};Controls.Add(portrait);
   AddLabel("ACTION JACKSON",108,23,248,28,16,FontStyle.Bold,ink);
   AddLabel("YOUR VOICE. YOUR MOUSE.",110,56,240,20,8,FontStyle.Bold,muted);
   var rule=new Panel{Location=new Point(18,104),Size=new Size(334,1),BackColor=Color.FromArgb(55,71,52)};Controls.Add(rule);
   status=AddLabel("PAUSED",20,120,330,28,17,FontStyle.Bold,muted);
   detail=AddLabel("Press Start when your game is visible.",21,153,328,36,9,FontStyle.Regular,muted);
   power=MakeButton("START LISTENING",20,197,330,43,green,Color.FromArgb(19,31,15));power.Font=new Font("Segoe UI",10F,FontStyle.Bold);
   power.Click+=(s,e)=>{if(active)Pause("Paused. Microphone and scanning are off.");else StartListening();};
   actionButtons=new Button[3];string[] names={"CALL","CHECK","FOLD"};
   for(int i=0;i<3;i++){int index=i;var b=MakeButton(names[i]+"  ·",20+i*113,251,104,35,panel,muted);b.Enabled=false;b.Click+=(s,e)=>Execute(names[index].ToLowerInvariant(),true);actionButtons[i]=b;}
   lastHeard=AddLabel(lastResult,21,295,330,21,9,FontStyle.Regular,ink);
   scanInfo=AddLabel("Screen reader off",21,318,330,18,8,FontStyle.Regular,muted);
   microphone=new ComboBox{Location=new Point(20,345),Size=new Size(281,24),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=panel,ForeColor=ink,FlatStyle=FlatStyle.Flat};
   foreach(var m in WaveInput.Devices())microphone.Items.Add(m);microphone.SelectedIndex=0;Controls.Add(microphone);
   microphone.DropDown+=(s,e)=>{var selected=microphone.SelectedItem as Microphone;string name=selected==null?"":selected.Name;microphone.Items.Clear();foreach(var m in WaveInput.Devices())microphone.Items.Add(m);microphone.SelectedIndex=0;for(int i=0;i<microphone.Items.Count;i++)if(((Microphone)microphone.Items[i]).Name==name)microphone.SelectedIndex=i;};
   for(int i=0;i<microphone.Items.Count;i++)if(((Microphone)microphone.Items[i]).Name.IndexOf("headset",StringComparison.OrdinalIgnoreCase)>=0)microphone.SelectedIndex=i;
   try{string preferred=File.ReadAllText(Path.Combine(Program.DirectoryPath,"microphone.txt"));for(int i=0;i<microphone.Items.Count;i++)if(((Microphone)microphone.Items[i]).Name==preferred)microphone.SelectedIndex=i;}catch{}
   meter=new ProgressBar{Location=new Point(308,346),Size=new Size(42,22),Maximum=100,Style=ProgressBarStyle.Continuous};Controls.Add(meter);
   test=new CheckBox{Text="Test voice (no clicks)",Location=new Point(21,378),Size=new Size(197,24),ForeColor=muted,FlatStyle=FlatStyle.Flat,Checked=voiceTest};Controls.Add(test);
   var practice=new LinkLabel{Text="Practice",Location=new Point(279,381),Size=new Size(71,20),LinkColor=green,ActiveLinkColor=ink};practice.Click+=(s,e)=>Process.Start(new ProcessStartInfo{FileName=Application.ExecutablePath,Arguments="--practice",UseShellExecute=true});Controls.Add(practice);
   AddLabel("Stop anytime: Ctrl + Alt + Space",21,409,330,16,8,FontStyle.Regular,muted);
   timer=new System.Windows.Forms.Timer{Interval=850};timer.Tick+=async(s,e)=>await ScanTick();
   Shown+=(s,e)=>{if(!Native.RegisterHotKey(Handle,42,0x0001|0x0002|0x4000,0x20))detail.Text="Press Start. Say 'stop listening' to pause.";if(voiceTest)StartListening();};
   FormClosing+=(s,e)=>{closing=true;Pause(null);Native.UnregisterHotKey(Handle,42);timer.Dispose();if(current!=null)current.Dispose();};
  }
  Label AddLabel(string text,int x,int y,int w,int h,float size,FontStyle style,Color color){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,style),ForeColor=color,AutoEllipsis=true};Controls.Add(l);return l;}
  Button MakeButton(string text,int x,int y,int w,int h,Color background,Color foreground){var b=new RoundedButton{Text=text,Location=new Point(x,y),Size=new Size(w,h),BackColor=background,ForeColor=foreground,Cursor=Cursors.Hand};Controls.Add(b);return b;}
  void Ui(Action a){if(closing||IsDisposed)return;try{BeginInvoke(a);}catch(InvalidOperationException){}}
  async void StartListening(){
   if(active||closing)return;
   power.Enabled=false;status.Text="STARTING";detail.Text="Connecting microphone and screen reader…";
   try{
    if(detector==null)detector=await Task.Run(()=>new Detector());
    int device=((Microphone)microphone.SelectedItem).Id;
    try{File.WriteAllText(Path.Combine(Program.DirectoryPath,"microphone.txt"),((Microphone)microphone.SelectedItem).Name);}catch{}
    var next=await Task.Run(()=>new Voice(device));
    if(closing){next.Dispose();return;}
    voice=next;int session=++generation;
    voice.Heard+=(word,confidence)=>Ui(()=>{if(active&&generation==session)OnHeard(word,confidence);});
    voice.Level+=level=>Ui(()=>{if(active&&generation==session)meter.Value=level;});
    voice.Feedback+=message=>Ui(()=>{if(active&&generation==session)lastHeard.Text=message;});
    voice.Fault+=error=>Ui(()=>{if(active&&generation==session)Pause(error);});
    stop=new CancellationTokenSource();active=true;microphone.Enabled=false;
    voice.Start();power.Text="PAUSE";power.BackColor=panel;power.ForeColor=ink;status.Text="LISTENING";status.ForeColor=green;
    detail.Text="Scanning the visible screen for buttons…";timer.Start();await ScanTick();
   }catch(Exception ex){try{File.WriteAllText(Path.Combine(Program.DirectoryPath,"last-error.txt"),ex.ToString());}catch{}Pause(ex.Message);}
   finally{if(!closing)power.Enabled=true;}
  }
  void Pause(string message){
   active=false;generation++;timer.Stop();if(stop!=null){stop.Cancel();stop=null;}
   var old=voice;voice=null;if(old!=null)Task.Run(()=>old.Dispose());
   if(closing)return;
   power.Text="START LISTENING";power.BackColor=green;power.ForeColor=Color.FromArgb(19,31,15);microphone.Enabled=true;meter.Value=0;
   status.Text="PAUSED";status.ForeColor=muted;detail.Text=message??"Paused.";scanInfo.Text="Screen reader off";
   foreach(var b in actionButtons){b.Enabled=false;b.ForeColor=muted;b.BackColor=panel;b.Text=b.Text.Split(' ')[0]+"  ·";}
  }
  void OnHeard(string text,float confidence){
   string word=text.ToLowerInvariant();bool prefixed=word.StartsWith("jackson ");if(prefixed)word=word.Substring(8);
   if(word=="stop listening"||word=="stop"){if(confidence>=0.45F)Pause("Paused by voice.");return;}
   if(confidence<(prefixed?0.55F:0.68F)){lastHeard.Text="Not clear enough — please repeat.";return;}
   lastHeard.Text="Heard: "+word.ToUpperInvariant()+"  ("+Math.Round(confidence*100)+"%)";
   if(test.Checked){lastHeard.Text+=" — no click";return;}Execute(word,false);
  }
  async Task ScanTick(){
   if(!active||busy||commandBusy||stop==null)return;
   if(test.Checked){detail.Text="Microphone test. Mouse clicks are disabled.";scanInfo.Text=voice==null?"Microphone off":voice.Diagnostic;return;}
   busy=true;int session=generation;var token=stop.Token;
   try{
    var result=await Task.Run(()=>detector.ScanAsync(token));
    if(!active||session!=generation){result.Dispose();return;}
    SetScan(result);
   }catch(OperationCanceledException){}catch(Exception ex){if(active&&session==generation){detail.Text="Screen scan unavailable: "+ex.Message;ClearTargets();}}
   finally{busy=false;}
  }
  void ClearTargets(){if(current!=null){current.Dispose();current=null;}foreach(var b in actionButtons){b.Enabled=false;b.BackColor=panel;}}
  void SetScan(Scan scan){
   if(current!=null)current.Dispose();current=scan;scanCount++;
   string[] names={"call","check","fold"};
   for(int i=0;i<names.Length;i++){
    int count=scan.Targets.Count(t=>t.Action==names[i]);
    actionButtons[i].Text=names[i].ToUpperInvariant()+(count==1?"  ✓":count>1?"  ?":"  ·");
    actionButtons[i].Enabled=count==1;actionButtons[i].ForeColor=count==1?green:muted;actionButtons[i].BackColor=count==1?Color.FromArgb(40,62,33):panel;
   }
   bool ambiguous=scan.Targets.GroupBy(t=>t.Action).Any(g=>g.Count()>1);
   detail.Text=ambiguous?"More than one matching button. Show one table.":scan.Targets.Count>0?"Buttons found. Say call, check, or fold.":"Looking for readable action buttons…";
   scanInfo.Text="Scan "+scanCount+"  ·  "+DateTime.Now.ToString("h:mm:ss tt")+"  ·  "+scan.Milliseconds+" ms";
  }
  async void Execute(string action,bool manual){
   if(!active||commandBusy||stop==null)return;
   if(Detector.ActionWord(action)==null)return;
   if((DateTime.UtcNow-lastCommand).TotalMilliseconds<1500){lastHeard.Text="Please wait a moment before repeating.";return;}
   commandBusy=true;lastCommand=DateTime.UtcNow;int session=generation;var token=stop.Token;
   try{
    lastHeard.Text=(manual?"Button: ":"Heard: ")+action+" — checking screen…";
    // Re-detect after the command. Cached scan coordinates are never used to click.
    var result=await Task.Run(()=>detector.ScanAsync(token));
    if(!active||session!=generation){result.Dispose();return;}
    SetScan(result);
    var matches=result.Targets.Where(t=>t.Action==action).ToList();
    if(matches.Count!=1){lastHeard.Text=matches.Count==0?"Heard "+action.ToUpperInvariant()+" — button not found.":"Heard "+action.ToUpperInvariant()+" — multiple buttons.";return;}
    var target=matches[0];
    if(test.Checked){lastHeard.Text="Test OK: "+action+" found. Mouse unchanged.";return;}
    if(!active||session!=generation||Native.BusyKeys()||!Detector.Fresh(target)){lastHeard.Text="Screen changed or mouse busy. Say it again.";return;}
    // Check the accessible enabled state again when the target is a native/DOM button.
    if(Native.Click(target.Center))lastHeard.Text="Clicked "+action.ToUpperInvariant()+"  ·  "+DateTime.Now.ToString("h:mm:ss");
    else lastHeard.Text="Windows blocked the click. No retry sent.";
   }catch(OperationCanceledException){}catch(Exception ex){if(active&&session==generation)lastHeard.Text="No click: "+ex.Message;}
   finally{commandBusy=false;}
  }
  protected override void WndProc(ref Message m){if(m.Msg==0x0312&&m.WParam.ToInt32()==42){Pause("Paused with Ctrl + Alt + Space.");return;}base.WndProc(ref m);}
 }
}
