using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ActionJackson {
 internal class PracticeForm : Form {
  public string LastAction="None";public int ClickCount;
  Label result;CheckBox drawn;Panel table;ComboBox sizes;
  public PracticeForm(){
   Text="Action Jackson — Practice Table";Icon=Program.LoadIcon();ClientSize=new Size(870,600);MinimumSize=new Size(610,420);
   BackColor=Color.FromArgb(17,27,25);ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",11);StartPosition=FormStartPosition.CenterScreen;
   var title=new Label{Text="PRACTICE TABLE",Location=new Point(30,23),AutoSize=true,Font=new Font("Segoe UI",21,FontStyle.Bold)};Controls.Add(title);
   var help=new Label{Text="No money. No game. Say a command and watch it register below.",Location=new Point(32,69),Size=new Size(790,45)};Controls.Add(help);
   result=new Label{Text="Waiting for a click…",Location=new Point(32,120),Size=new Size(800,40),Font=new Font("Segoe UI",18,FontStyle.Bold),ForeColor=Color.FromArgb(155,216,96)};Controls.Add(result);
   drawn=new CheckBox{Text="Canvas buttons",Location=new Point(32,179),Size=new Size(210,28),Checked=false};drawn.CheckedChanged+=(s,e)=>BuildTable();Controls.Add(drawn);
   sizes=new ComboBox{Location=new Point(255,179),Size=new Size(150,28),DropDownStyle=ComboBoxStyle.DropDownList};sizes.Items.AddRange(new object[]{"Small labels","Medium labels","Large labels"});sizes.SelectedIndex=1;sizes.SelectedIndexChanged+=(s,e)=>BuildTable();Controls.Add(sizes);
   table=new Panel{Location=new Point(28,240),Size=new Size(810,315),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom,BackColor=Color.FromArgb(22,62,49)};Controls.Add(table);
   table.Resize+=(s,e)=>BuildTable();BuildTable();
  }
  void BuildTable(){
   if(table==null)return;
   while(table.Controls.Count>0){var c=table.Controls[0];table.Controls.RemoveAt(0);c.Dispose();}
   string[] labels={"Fold","Check","Call $20"};int font=sizes.SelectedIndex==0?10:sizes.SelectedIndex==2?25:17;
   int width=Math.Min(220,(table.Width-50)/3),height=70,start=(table.Width-(width*3+24))/2;
   for(int i=0;i<3;i++){
    string action=i==0?"fold":i==1?"check":"call";
    Control b;
    if(drawn.Checked)b=new PaintedButton(labels[i]);
    else b=new RoundedButton{Text=labels[i],BackColor=Color.FromArgb(59,89,68),ForeColor=Color.White};
    b.Font=new Font("Segoe UI",font,FontStyle.Bold);b.Bounds=new Rectangle(start+i*(width+12),Math.Max(30,table.Height-120),width,height);
    b.Click+=(s,e)=>Record(action);table.Controls.Add(b);
   }
  }
  internal void Record(string action){LastAction=action;ClickCount++;result.Text="Received: "+action.ToUpperInvariant()+"    /    Clicks: "+ClickCount;}
 }
 internal class PaintedButton : Control {
  public PaintedButton(string text){Text=text;BackColor=Color.FromArgb(59,89,68);ForeColor=Color.White;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Graphic;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?Color.Black:Parent.BackColor);using(var path=Shapes.Rounded(new RectangleF(1,1,Width-3,Height-3),20))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(Color.FromArgb(106,139,113),2)){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);}
 }
 internal static class Tests {
  internal static async Task Run(string[] args){
   var lines=new List<string>();int failures=0;
   Action<bool,string> check=(ok,label)=>{lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;};
   try{
    var d=new Detector();
    foreach(int size in new[]{9,12,18,28,42,64})foreach(bool light in new[]{false,true}){
     int width=Math.Max(110,size*5),height=Math.Max(48,size*2),gap=20;
     using(var bitmap=new Bitmap(width*3+gap*4,height+gap*2,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel)){
      g.Clear(light?Color.WhiteSmoke:Color.FromArgb(22,62,49));
      string[] names={"Fold","Check","Call $20"};
      for(int i=0;i<3;i++){
       var rect=new Rectangle(gap+i*(width+gap),gap,width,height);
       using(var fill=new SolidBrush(light?Color.FromArgb(225,229,237):Color.FromArgb(59,89,68)))g.FillRectangle(fill,rect);
       TextRenderer.DrawText(g,names[i],font,rect,light?Color.Black:Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
      }
      var targets=await d.ReadVariants(bitmap,Point.Empty,1);
      // The enlarged scan is also used by the live scanner for tiny text.
      if(targets.Count<3)using(var large=Detector.Resize(bitmap,2.0))targets=await d.ReadVariants(large,Point.Empty,2.0);
      bool found=new[]{"call","check","fold"}.All(a=>targets.Any(t=>t.Action==a));
      check(found,"OCR "+size+" px, "+(light?"light":"dark")+": "+string.Join(",",targets.Select(t=>t.Action))+" ["+d.LastText+"]");
      if(found)check(targets.All(t=>!Detector.FindButton(bitmap,t.TextBounds,Point.Empty).IsEmpty),"Button shapes "+size+" px, "+(light?"light":"dark"));
      if(size==18)bitmap.Save(Path.Combine(Path.GetDirectoryName(args[args.Length-1]),"fixture-"+(light?"light":"dark")+".png"));
     }
    }
    check(!Detector.ButtonLine("Call me and check the folder"),"Reject sentence text");
    check(!Detector.ButtonLine("Check/Fold")&&!Detector.ButtonLine("Call any")&&!Detector.ButtonLine("Auto fold"),"Reject combined and automatic-action labels");
    if(!args.Contains("--ocr-only")){
    using(var synth=new SpeechSynthesizer()){
     foreach(string command in new[]{"call","check","fold","Jackson call","stop listening"}){
      using(var wav=new MemoryStream())using(var speech=Voice.CreateEngine()){
       synth.SetOutputToWaveStream(wav);synth.Speak(command);synth.SetOutputToNull();wav.Position=0;speech.SetInputToWaveStream(wav);
       var recognition=speech.Recognize(TimeSpan.FromSeconds(5));
       check(recognition!=null&&recognition.Text.Equals(command,StringComparison.OrdinalIgnoreCase),"Speech pipeline: "+command+(recognition==null?" (none)":" → "+recognition.Text+" / "+recognition.Confidence.ToString("0.00")));
      }
     }
     if(Directory.Exists(LocalRecognizer.ModelPath))foreach(string command in new[]{"call","check","fold"}){
      using(var raw=new MemoryStream())using(var local=new LocalRecognizer()){
       synth.SetOutputToAudioStream(raw,new System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,System.Speech.AudioFormat.AudioBitsPerSample.Sixteen,System.Speech.AudioFormat.AudioChannel.Mono));synth.Speak(command);synth.SetOutputToNull();
       var data=raw.ToArray();local.Feed(data,data.Length);var json=local.Result(true);check(LocalRecognizer.Text(json,"text")==command,"Offline recognition: "+command+" → "+LocalRecognizer.Text(json,"text"));
      }
     }
    }
    var microphones=WaveInput.Devices();check(microphones.Count>1,"Microphone devices: "+string.Join(", ",microphones.Select(m=>m.Name)));
    using(var audio=new WaveInput(-1)){var bytes=new byte[1024];int count=await Task.Run(()=>audio.Read(bytes,0,bytes.Length));check(count>0,"Default microphone produces PCM audio");}
    var headset=microphones.FirstOrDefault(m=>m.Name.IndexOf("headset",StringComparison.OrdinalIgnoreCase)>=0)??microphones[0];
    using(var voice=new Voice(headset.Id)){string fault=null;voice.Fault+=message=>fault=message;voice.Start();await Task.Delay(2500);check(fault==null,"Live speech recognizer stays active with "+headset.Name+" "+fault);}
    }
   }catch(Exception ex){failures++;lines.Add("FAIL "+ex);}
   lines.Add("Failures: "+failures);string path=args.Length>1?args[args.Length-1]:Path.Combine(Program.DirectoryPath,"self-test.txt");File.WriteAllLines(path,lines);Environment.ExitCode=failures==0?0:1;
  }
 }
 internal class IntegrationForm : PracticeForm {
  public IntegrationForm(){Text="Action Jackson — Integration Test";Shown+=async(s,e)=>await Run();}
  async Task Run(){
   var report=new List<string>();
   try{
    await Task.Delay(600);var detector=new Detector();
    // Read a visible test surface, and use the production mouse path to click it.
    foreach(string action in new[]{"fold","check","call"}){
     using(var scan=await Task.Run(()=>detector.ScanAsync(CancellationToken.None))){
      // The production scanner excludes its own process; integration OCR is checked below directly.
      report.Add("Scan completed in "+scan.Milliseconds+" ms");
     }
     var region=Native.Bounds(Handle);
     using(var bitmap=Detector.Capture(region)){
      var words=await detector.ReadBitmap(bitmap,region.Location,1);var target=words.FirstOrDefault(t=>t.Action==action);
      if(target==null)throw new Exception("Test action not read: "+action);
      if(!Native.Click(target.Center))throw new Exception("SendInput blocked");
     }
     await Task.Delay(300);
     if(LastAction!=action)throw new Exception("Click did not reach "+action);
     report.Add("PASS physical mouse click: "+action);
    }
   }catch(Exception ex){report.Add("FAIL "+ex);}
   File.WriteAllLines(Path.Combine(Program.DirectoryPath,"integration-test.txt"),report);Close();
  }
 }
}
