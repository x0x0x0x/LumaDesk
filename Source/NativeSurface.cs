using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LumaDesk {
// Per-pixel alpha avoids the binary clipping of a Form.Region.
public static class NativeSurface {
 [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;public Point(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct Size {public int Width,Height;public Size(int w,int h){Width=w;Height=h;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend {public byte Operation,Flags,Opacity,Alpha;}
 [StructLayout(LayoutKind.Sequential)]struct Header {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int Xppm,Yppm;public uint Used,Important;}
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr screen,ref Point dst,ref Size size,IntPtr source,ref Point src,uint key,ref Blend blend,uint flags);
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateDIBSection(IntPtr dc,ref Header info,uint usage,out IntPtr bits,IntPtr section,uint offset);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 public static void Present(IntPtr window,Rectangle bounds,Bitmap image,byte opacity){
 IntPtr screen=IntPtr.Zero,dc=IntPtr.Zero,dib=IntPtr.Zero,previous=IntPtr.Zero;
 try{screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());dc=CreateCompatibleDC(screen);if(dc==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
 var info=new Header{Size=40,Width=image.Width,Height=-image.Height,Planes=1,Bits=32};IntPtr pixels;dib=CreateDIBSection(dc,ref info,0,out pixels,IntPtr.Zero,0);if(dib==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
 var locked=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{int rowBytes=image.Width*4;var row=new byte[rowBytes];for(int y=0;y<image.Height;y++){Marshal.Copy(IntPtr.Add(locked.Scan0,y*locked.Stride),row,0,rowBytes);Marshal.Copy(row,0,IntPtr.Add(pixels,y*rowBytes),rowBytes);}}finally{image.UnlockBits(locked);}
 previous=SelectObject(dc,dib);var dst=new Point(bounds.X,bounds.Y);var src=new Point(0,0);var size=new Size(image.Width,image.Height);var blend=new Blend{Operation=0,Flags=0,Opacity=opacity,Alpha=1};if(!UpdateLayeredWindow(window,screen,ref dst,ref size,dc,ref src,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
 }finally{if(previous!=IntPtr.Zero&&dc!=IntPtr.Zero)SelectObject(dc,previous);if(dib!=IntPtr.Zero)DeleteObject(dib);if(dc!=IntPtr.Zero)DeleteDC(dc);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);}}
}
}
