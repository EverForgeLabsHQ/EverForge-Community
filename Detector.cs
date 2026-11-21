using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ActionJackson {
 internal sealed class Target : IDisposable {
  public string Action,Source;
  public Rectangle TextBounds,ButtonBounds,WindowBounds,PatchBounds;
  public IntPtr Window;
  public Bitmap Patch;
  public Point Center {get{return new Point(ButtonBounds.Left+ButtonBounds.Width/2,ButtonBounds.Top+ButtonBounds.Height/2);}}
  public void Dispose(){if(Patch!=null){Patch.Dispose();Patch=null;}}
 }
 internal sealed class Scan : IDisposable {
  public readonly List<Target> Targets=new List<Target>();
  public DateTime Time=DateTime.UtcNow;
  public long Milliseconds;
  public void Dispose(){foreach(var t in Targets)t.Dispose();}
 }
 internal sealed class Detector {
  OcrEngine engine;
  internal string LastText;
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  readonly uint ownId=(uint)Process.GetCurrentProcess().Id;
  public Detector(){engine=OcrEngine.TryCreateFromLanguage(new Language("en-US"));if(engine==null)throw new InvalidOperationException("Windows English text recognition is unavailable.");}
  internal static string ActionWord(string s){
   var t=Regex.Replace(s.ToLowerInvariant(),@"^[^a-z]+|[^a-z]+$","");
   return t=="call"||t=="check"||t=="fold"?t:null;
  }
  internal static bool ButtonLine(string s){
   string t=s.ToLowerInvariant();
   if(Regex.IsMatch(t,@"\b(auto|any|next|don't|do not|to|and|or)\b")||t.Contains("/"))return false;
   t=Regex.Replace(t,@"\b(call|check|fold|raise|bet|all\s*-?\s*in|bb|chips)\b","");
   t=Regex.Replace(t,@"\b[sS](?=\d)","");
   t=Regex.Replace(t,@"[\d\s\p{Sc}.,:+\-()\[\]kmb]","");
   return t.Length==0;
  }
  static async Task<OcrResult> Read(OcrEngine e,Bitmap image){
   var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   byte[] bytes=new byte[image.Width*image.Height*4];
   try {for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),bytes,y*image.Width*4,image.Width*4);} finally{image.UnlockBits(bits);}
   // GDI text and desktop captures can carry zero alpha even for visible pixels.
   for(int i=3;i<bytes.Length;i+=4)bytes[i]=255;
   using(var writer=new DataWriter())using(var sb=new SoftwareBitmap(BitmapPixelFormat.Bgra8,image.Width,image.Height,BitmapAlphaMode.Ignore)){
    writer.WriteBytes(bytes);sb.CopyFromBuffer(writer.DetachBuffer());
    var operation=e.RecognizeAsync(sb);
    try {while(operation.Status==Windows.Foundation.AsyncStatus.Started)await Task.Delay(8).ConfigureAwait(false);return operation.GetResults();}finally{operation.Close();}
   }
  }
  internal async Task<List<Target>> ReadBitmap(Bitmap image,Point origin,double scale){
   var result=await Read(engine,image).ConfigureAwait(false);
   LastText=string.Join(" | ",result.Lines.Select(l=>l.Text));
   var words=new List<Target>();
   foreach(var line in result.Lines){
    if(!ButtonLine(line.Text))continue;
    foreach(var w in line.Words){
     var action=ActionWord(w.Text);if(action==null)continue;
     var r=w.BoundingRect;
     var rect=new Rectangle(origin.X+(int)Math.Round(r.X/scale),origin.Y+(int)Math.Round(r.Y/scale),Math.Max(1,(int)Math.Round(r.Width/scale)),Math.Max(1,(int)Math.Round(r.Height/scale)));
     words.Add(new Target{Action=action,TextBounds=rect,ButtonBounds=rect,Source="screen"});
    }
   }
   return words;
  }
  internal async Task<List<Target>> ReadVariants(Bitmap image,Point origin,double scale){
   var found=await ReadBitmap(image,origin,scale).ConfigureAwait(false);
   string debug=LastText;
   if(found.Select(t=>t.Action).Distinct().Count()<3){
    using(var alternate=Invert(image)){
     var extra=await ReadBitmap(alternate,origin,scale).ConfigureAwait(false);
     debug+=" / inverted: "+LastText;
     foreach(var t in extra)if(!found.Any(o=>o.Action==t.Action&&Near(o.Center,t.Center,Math.Max(12,t.TextBounds.Height))))found.Add(t);
     if(found.Select(t=>t.Action).Distinct().Count()<3)using(var thinned=ThinBright(alternate)){
      var fine=await ReadBitmap(thinned,origin,scale).ConfigureAwait(false);
      debug+=" / thin: "+LastText;
      foreach(var t in fine)if(!found.Any(o=>o.Action==t.Action&&Near(o.Center,t.Center,Math.Max(12,t.TextBounds.Height))))found.Add(t);
     }
    }
   }
   LastText=debug;return found;
  }
  static Bitmap Invert(Bitmap source){
   var output=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
   var src=source.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   var dst=output.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try{var row=new byte[source.Width*4];for(int y=0;y<source.Height;y++){Marshal.Copy(IntPtr.Add(src.Scan0,y*src.Stride),row,0,row.Length);for(int i=0;i<row.Length;i+=4){byte gray=(byte)(255-(row[i]*29+row[i+1]*150+row[i+2]*77)/256);row[i]=gray;row[i+1]=gray;row[i+2]=gray;row[i+3]=255;}Marshal.Copy(row,0,IntPtr.Add(dst.Scan0,y*dst.Stride),row.Length);}}finally{source.UnlockBits(src);output.UnlockBits(dst);}return output;
  }
  static Bitmap ThinBright(Bitmap source){
   int width=source.Width,height=source.Height;var output=new Bitmap(width,height,PixelFormat.Format32bppArgb);
   var src=source.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   var dst=output.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try{
    var bytes=new byte[width*height*4];for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(src.Scan0,y*src.Stride),bytes,y*width*4,width*4);
    var result=(byte[])bytes.Clone();for(int y=1;y<height-1;y++)for(int x=1;x<width-1;x++){int index=(y*width+x)*4;byte value=bytes[index];value=Math.Min(value,Math.Min(bytes[index-4],bytes[index+4]));value=Math.Min(value,Math.Min(bytes[index-width*4],bytes[index+width*4]));result[index]=result[index+1]=result[index+2]=value;result[index+3]=255;}
    for(int y=0;y<height;y++)Marshal.Copy(result,y*width*4,IntPtr.Add(dst.Scan0,y*dst.Stride),width*4);
   }finally{source.UnlockBits(src);output.UnlockBits(dst);}return output;
  }
  internal static Bitmap Capture(Rectangle bounds){
   var b=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
   try{using(var g=Graphics.FromImage(b))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);return b;}catch{b.Dispose();throw;}
  }
  internal async Task<Scan> ScanAsync(CancellationToken cancellation){
   await gate.WaitAsync(cancellation).ConfigureAwait(false);
   var scan=new Scan();var timer=Stopwatch.StartNew();
   try {
    foreach(var screen in Screen.AllScreens){
     cancellation.ThrowIfCancellationRequested();
     Rectangle bounds=screen.Bounds;
     using(var original=Capture(bounds)){
      double factor=Math.Min(1.0,Math.Min(2400.0/bounds.Width,(double)OcrEngine.MaxImageDimension/Math.Max(bounds.Width,bounds.Height)));
      var candidates=new List<Target>();
      using(var resized=Resize(original,factor))candidates.AddRange(await ReadVariants(resized,bounds.Location,factor).ConfigureAwait(false));
      // A second, enlarged pass helps small labels after shrinking a game window.
      if(candidates.Select(t=>t.Action).Distinct().Count()<2){
       double enlarged=Math.Min(2.0,(double)OcrEngine.MaxImageDimension/Math.Max(bounds.Width,bounds.Height));
       if(enlarged>factor+0.1)using(var resized=Resize(original,enlarged)){
        var small=await ReadVariants(resized,bounds.Location,enlarged).ConfigureAwait(false);
        foreach(var t in small)if(!candidates.Any(o=>o.Action==t.Action&&Near(o.Center,t.Center,Math.Max(12,t.TextBounds.Height))))candidates.Add(t);
       }
      }
      AddAccessibleButtons(candidates,bounds);
      foreach(var t in candidates){
       cancellation.ThrowIfCancellationRequested();
       var center=t.Center;t.Window=Native.RootAt(center);uint pid;
       Native.GetWindowThreadProcessId(t.Window,out pid);
       if(t.Window==IntPtr.Zero||pid==ownId||!Native.IsWindowEnabled(t.Window))continue;
       t.WindowBounds=Native.Bounds(t.Window);
       bool rejected=false;
       bool accessible=AccessibleButton(center,t,out rejected);
       if(rejected)continue;
       if(!accessible){
        // OCR alone must have a button-shaped background; a word in a paragraph is not a target.
        Rectangle button=FindButton(original,t.TextBounds,bounds.Location);
        if(button.IsEmpty)continue;
        t.ButtonBounds=button;
        // Require another action in the same local row, or an especially clear isolated button.
        bool sibling=candidates.Any(o=>o!=t&&o.Action!=t.Action&&Math.Abs(o.Center.Y-center.Y)<Math.Max(40,t.TextBounds.Height*2.5)&&Math.Abs(o.Center.X-center.X)<Math.Max(900,t.TextBounds.Height*30)&&Native.RootAt(o.Center)==t.Window);
        if(!sibling)continue;
       }
       if(!t.ButtonBounds.Contains(t.TextBounds.Location)||Native.RootAt(t.Center)!=t.Window)continue;
       t.PatchBounds=Rectangle.Intersect(Rectangle.Inflate(t.TextBounds,5,5),bounds);
       var local=t.PatchBounds;local.Offset(-bounds.X,-bounds.Y);
       t.Patch=original.Clone(local,PixelFormat.Format32bppArgb);
       scan.Targets.Add(t);
      }
     }
    }
    scan.Time=DateTime.UtcNow;scan.Milliseconds=timer.ElapsedMilliseconds;return scan;
   }catch{scan.Dispose();throw;}finally{gate.Release();}
  }
  static bool Near(Point a,Point b,int tolerance){return Math.Abs(a.X-b.X)<tolerance&&Math.Abs(a.Y-b.Y)<tolerance;}
  void AddAccessibleButtons(List<Target> targets,Rectangle screen){
   // Once a row is found visually, read its native/DOM buttons too. This keeps
   // hover styles, small fonts, and keyboard focus from hiding a readable label.
   var windows=targets.Select(t=>Native.RootAt(t.Center)).Concat(new[]{Native.GetForegroundWindow()}).Where(h=>h!=IntPtr.Zero).Distinct().ToArray();
   foreach(var window in windows){
    uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid==ownId)continue;
    try{
     var root=AutomationElement.FromHandle(window);
     var buttons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
     foreach(AutomationElement element in buttons){
      var c=element.Current;if(!c.IsEnabled||c.IsOffscreen||!ButtonLine(c.Name))continue;
      var names=Regex.Matches(c.Name.ToLowerInvariant(),@"\b(call|check|fold)\b").Cast<Match>().Select(m=>m.Value).Distinct().ToArray();
      if(names.Length!=1)continue;
      var r=c.BoundingRectangle;if(r.Width<10||r.Width>700||r.Height<10||r.Height>300)continue;
      var rectangle=new Rectangle((int)r.X,(int)r.Y,(int)r.Width,(int)r.Height);var center=new Point(rectangle.Left+rectangle.Width/2,rectangle.Top+rectangle.Height/2);
      if(!screen.Contains(rectangle)||Native.RootAt(center)!=window||targets.Any(t=>t.Action==names[0]&&rectangle.Contains(t.Center)))continue;
      targets.Add(new Target{Action=names[0],TextBounds=rectangle,ButtonBounds=rectangle,Source="button"});
     }
    }catch{}
   }
  }
  internal static Bitmap Resize(Bitmap input,double scale){
   // Desktop/GDI bitmaps have usable RGB even where their alpha bytes are zero.
   // Normalize BEFORE scaling, otherwise GDI+ can erase dark lettering.
   var data=input.LockBits(new Rectangle(0,0,input.Width,input.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   try{var row=new byte[input.Width*4];for(int y=0;y<input.Height;y++){IntPtr address=IntPtr.Add(data.Scan0,y*data.Stride);Marshal.Copy(address,row,0,row.Length);for(int i=3;i<row.Length;i+=4)row[i]=255;Marshal.Copy(row,0,address,row.Length);}}finally{input.UnlockBits(data);}
   var b=new Bitmap(Math.Max(1,(int)Math.Round(input.Width*scale)),Math.Max(1,(int)Math.Round(input.Height*scale)),PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(input,new Rectangle(0,0,b.Width,b.Height));}return b;
  }
  static bool AccessibleButton(Point p,Target target,out bool rejected){
   rejected=false;
   try {
    var el=AutomationElement.FromPoint(new System.Windows.Point(p.X,p.Y));
    for(int i=0;el!=null&&i<5;i++,el=TreeWalker.ControlViewWalker.GetParent(el)){
     var c=el.Current;
     if(c.ControlType==ControlType.CheckBox||c.ControlType==ControlType.RadioButton){rejected=true;return false;}
     if(c.ControlType==ControlType.Button){
      if(!c.IsEnabled||c.IsOffscreen||!ButtonLine(c.Name)||!Regex.IsMatch(c.Name,@"\b"+target.Action+@"\b",RegexOptions.IgnoreCase)){rejected=true;return false;}
      var r=c.BoundingRectangle;
      if(r.Width<10||r.Height<10||r.Width>700||r.Height>300){rejected=true;return false;}
      target.ButtonBounds=new Rectangle((int)r.X,(int)r.Y,(int)r.Width,(int)r.Height);target.Source="button";return true;
     }
     if(c.ControlType==ControlType.Window)break;
    }
   }catch{}
   return false;
  }
  static int Delta(Color a,Color b){return Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)));}
  internal static Rectangle FindButton(Bitmap b,Rectangle global,Point origin){
   Rectangle t=global;t.Offset(-origin.X,-origin.Y);
   int cx=t.Left+t.Width/2;
   // Trace the padding immediately below/above the lettering to a distinct rectangular boundary.
   foreach(int y in new[]{t.Bottom+3,t.Top-3,t.Bottom+6}){
    if(y<1||y>=b.Height-1||cx<1||cx>=b.Width-1)continue;
    Color color=b.GetPixel(cx,y);
    int left=cx,right=cx;
    int maxWidth=Math.Min(1000,Math.Max(450,t.Width*10));
    while(left>1&&cx-left<maxWidth&&Delta(b.GetPixel(left-1,y),color)<18)left--;
    while(right<b.Width-2&&right-cx<maxWidth&&Delta(b.GetPixel(right+1,y),color)<18)right++;
    int width=right-left;
    if(left>=t.Left-2||right<=t.Right+2||width<t.Width+10||width>maxWidth)continue;
    int sx=left+Math.Max(3,(t.Left-left)/2);
    int up=y,down=y;
    while(up>1&&y-up<220&&Delta(b.GetPixel(sx,up-1),color)<18)up--;
    while(down<b.Height-2&&down-y<220&&Delta(b.GetPixel(sx,down+1),color)<18)down++;
    int height=down-up;
    if(up>t.Top-2||down<t.Bottom+2||height<t.Height+6||height>Math.Max(150,t.Height*5)||width<height)continue;
    // Reject screen edges and background expanses without a visible button edge.
    if(left<=2||right>=b.Width-3||up<=2||down>=b.Height-3)continue;
    return new Rectangle(origin.X+left,origin.Y+up,width+1,height+1);
   }
   return Rectangle.Empty;
  }
  internal static bool Fresh(Target t){
   if(!Native.IsWindow(t.Window)||Native.IsIconic(t.Window)||Native.Bounds(t.Window)!=t.WindowBounds||Native.RootAt(t.Center)!=t.Window||t.Patch==null)return false;
   if(t.Source=="button"){bool rejected;var verify=new Target{Action=t.Action};if(!AccessibleButton(t.Center,verify,out rejected)||rejected||verify.ButtonBounds!=t.ButtonBounds)return false;}
   using(var now=Capture(t.PatchBounds)){
    long sum=0;int n=0;
    for(int y=0;y<now.Height;y+=2)for(int x=0;x<now.Width;x+=2){sum+=Delta(now.GetPixel(x,y),t.Patch.GetPixel(x,y));n++;}
    return n>0&&sum/(double)n<7.0;
   }
  }
 }
}
