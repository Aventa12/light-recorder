using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScreenRecorderLib;

namespace LightRecorder {
 internal sealed class CaptureRequest {
  public string DeviceName, FilePath, DiagnosticLog; public Rectangle Bounds, Crop; public IntPtr Window;
  public bool SystemAudio, Microphone, Highlight, Hardware=true; public int Fps=30, MaxHeight=1080;
 }
 internal sealed class RecorderEngine : IDisposable {
  Recorder recorder; public readonly Stopwatch Clock=new Stopwatch();
  public RecorderStatus Status {get;private set;}
  public event Action<RecorderStatus> StatusChanged;
  public event Action<string> Completed, Failed;
  public void Start(CaptureRequest request) {
   if(recorder!=null)throw new InvalidOperationException("上一段录制还未结束。");
   if(File.Exists(request.FilePath))throw new IOException("视频文件已存在，请使用新的文件名。");
   var sources=new List<RecordingSourceBase>();
   int w=request.Bounds.Width,h=request.Bounds.Height;
   if(request.Window!=IntPtr.Zero) {
    if(!Native.IsWindow(request.Window)||Native.IsIconic(request.Window))throw new InvalidOperationException("所选窗口已关闭或最小化，请重新选择并保持窗口展开。");
    sources.Add(new WindowRecordingSource(request.Window));
   } else {
    var display=new DisplayRecordingSource(request.DeviceName) {RecorderApi=RecorderApi.WindowsGraphicsCapture};
    if(!request.Crop.IsEmpty) {
     display.SourceRect=new ScreenRect(request.Crop.Left-request.Bounds.Left,request.Crop.Top-request.Bounds.Top,request.Crop.Width,request.Crop.Height);
     w=request.Crop.Width;h=request.Crop.Height;
    }
    sources.Add(display);
   }
   double scale=Math.Min(1,request.MaxHeight/(double)Math.Max(1,h));
   int outputW=Math.Max(2,(int)(w*scale)/2*2),outputH=Math.Max(2,(int)(h*scale)/2*2);
   var audio=new AudioOptions {IsAudioEnabled=request.SystemAudio||request.Microphone,Bitrate=AudioBitrate.bitrate_128kbps};
   if(request.SystemAudio)audio.AudioSources.Add(LoopbackAudioSource.Default);
   if(request.Microphone)audio.AudioSources.Add(CaptureAudioSource.Default);
   var options=new RecorderOptions {
    SourceOptions=new SourceOptions {RecordingSources=sources},
    OutputOptions=new OutputOptions {RecorderMode=RecorderMode.Video,OutputFrameSize=new ScreenSize(outputW,outputH),Stretch=StretchMode.Uniform},
    AudioOptions=audio,
    MouseOptions=new MouseOptions {IsMousePointerEnabled=true,IsMouseClicksDetected=request.Highlight,MouseClickDetectionRadius=22,MouseClickDetectionDuration=220,MouseLeftClickDetectionColor="#58DDB0",MouseRightClickDetectionColor="#FFBD66"},
    VideoEncoderOptions=new VideoEncoderOptions {Framerate=request.Fps,Bitrate=(int)Math.Max(500000,Math.Min(16000000,(request.Fps==60?8000000.0:4000000.0)*outputW*outputH/(1920.0*1080))),Encoder=new H264VideoEncoder {BitrateMode=H264BitrateControlMode.CBR},IsHardwareEncodingEnabled=request.Hardware,IsFragmentedMp4Enabled=true,IsMp4FastStartEnabled=false}
   };
   if(!string.IsNullOrEmpty(request.DiagnosticLog))options.LogOptions=new LogOptions {IsLogEnabled=true,LogFilePath=request.DiagnosticLog,LogSeverityLevel=LogLevel.Debug};
   Directory.CreateDirectory(Path.GetDirectoryName(request.FilePath));
   Clock.Reset(); recorder=Recorder.CreateRecorder(options);
   recorder.OnStatusChanged+=delegate(object s,RecordingStatusEventArgs e) {
    Status=e.Status;
    if(e.Status==RecorderStatus.Recording)Clock.Start();else Clock.Stop();
    var handler=StatusChanged;if(handler!=null)handler(e.Status);
   };
   recorder.OnRecordingComplete+=delegate(object s,RecordingCompleteEventArgs e) {Clock.Stop();var handler=Completed;if(handler!=null)handler(e.FilePath);};
   recorder.OnRecordingFailed+=delegate(object s,RecordingFailedEventArgs e) {Clock.Stop();var handler=Failed;if(handler!=null)handler(e.Error);};
   try {recorder.Record(request.FilePath);}catch {Dispose();throw;}
  }
  public void Pause() {if(recorder!=null&&Status==RecorderStatus.Recording)recorder.Pause();}
  public void Resume() {if(recorder!=null&&Status==RecorderStatus.Paused)recorder.Resume();}
  public void Stop() {if(recorder!=null)recorder.Stop();}
  public void Dispose() {Clock.Stop();if(recorder!=null){recorder.Dispose();recorder=null;}Status=RecorderStatus.Idle;}
 }
}
