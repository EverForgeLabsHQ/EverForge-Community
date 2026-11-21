using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ActionJackson {
 internal static class Native {
  [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
  [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; public Rectangle Rectangle {get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}} }
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }
  [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION U; }
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int command);
  [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flags);
  [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
  [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] input,int size);
  [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  internal static void SetDpi(){try{if(!SetProcessDpiAwarenessContext(new IntPtr(-4)))SetProcessDPIAware();}catch{SetProcessDPIAware();}}
  internal static IntPtr RootAt(Point p){return GetAncestor(WindowFromPoint(new POINT(p.X,p.Y)),2);}
  internal static string Title(IntPtr h){var b=new StringBuilder(256);GetWindowText(h,b,256);return b.ToString();}
  internal static Rectangle Bounds(IntPtr h){RECT r;return GetWindowRect(h,out r)?r.Rectangle:Rectangle.Empty;}
  internal static bool BusyKeys(){foreach(int k in new[]{1,2,4,16,17,18,91,92})if((GetAsyncKeyState(k)&0x8000)!=0)return true;return false;}
  internal static bool Click(Point p){
   POINT original;GetCursorPos(out original);
   var v=SystemInformation.VirtualScreen;
   int x=(int)Math.Round((p.X-v.Left)*65535.0/Math.Max(1,v.Width-1));
   int y=(int)Math.Round((p.Y-v.Top)*65535.0/Math.Max(1,v.Height-1));
   var inputs=new INPUT[3];
   inputs[0].U.mi=new MOUSEINPUT{dx=x,dy=y,dwFlags=0x8000|0x4000|1};
   inputs[1].U.mi=new MOUSEINPUT{dwFlags=2};inputs[2].U.mi=new MOUSEINPUT{dwFlags=4};
   uint sent=SendInput(3,inputs,Marshal.SizeOf(typeof(INPUT)));
   if(sent!=3){var release=new INPUT[1];release[0].U.mi=new MOUSEINPUT{dwFlags=4};SendInput(1,release,Marshal.SizeOf(typeof(INPUT)));return false;}
   System.Threading.Thread.Sleep(45);
   POINT current;if(GetCursorPos(out current)&&Math.Abs(current.X-p.X)<3&&Math.Abs(current.Y-p.Y)<3)SetCursorPos(original.X,original.Y);
   return true;
  }
 }
}
