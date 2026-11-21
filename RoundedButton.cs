using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ActionJackson {
 internal static class Shapes {
  internal static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 }
 internal class RoundedButton : Button {
  bool hover,pressed;
  public RoundedButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>0&&Height>0)using(var path=Shapes.Rounded(new RectangleF(0,0,Width,Height),12)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   Color color=BackColor;if(Enabled&&hover){int add=pressed?-8:8;color=Color.FromArgb(Math.Max(0,Math.Min(255,color.R+add)),Math.Max(0,Math.Min(255,color.G+add)),Math.Max(0,Math.Min(255,color.B+add)));}
   using(var path=Shapes.Rounded(new RectangleF(0,0,Width-1,Height-1),12))using(var brush=new SolidBrush(color))e.Graphics.FillPath(brush,path);
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(119,139,112),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  }
 }
}
