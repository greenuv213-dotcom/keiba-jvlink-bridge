// Build locally with the Windows .NET Framework compiler. SDK binaries and keys
// are not bundled. Probe never constructs a COM instance or opens a network.
using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

public static class JvLinkProbe {
 const string ProgId = "JVDTLab.JVLink";
 static JavaScriptSerializer Json = new JavaScriptSerializer();
 static bool Registered(RegistryView view) {
  using (var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view))
  using (var key = root.OpenSubKey(ProgId + "\\CLSID")) return key != null;
 }
 static object Call(object link, string method, params object[] args) {
  return link.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, link, args, CultureInfo.InvariantCulture);
 }
 static int ReadBytes(object link, out byte[] bytes) {
  const int capacity=110000;
  object[] values={new byte[capacity],capacity,""};
  // SDK 5.0.0 JVGets: VARIANT* buffer, LONG size, BSTR* filename.
  // Returning bytes avoids a Shift_JIS -> BSTR -> Shift_JIS round trip.
  var byRef=new ParameterModifier(3);byRef[0]=true;byRef[2]=true;
  int count=Convert.ToInt32(link.GetType().InvokeMember("JVGets",BindingFlags.InvokeMethod,null,link,values,new[]{byRef},CultureInfo.InvariantCulture,null));
  bytes=null;
  if(count<=0)return count;
  Array source=values[0] as Array;
  if(count>capacity||source==null||source.Rank!=1||source.Length<count)throw new InvalidOperationException("Invalid JVGets byte buffer.");
  bytes=new byte[count];
  int lower=source.GetLowerBound(0);
  for(int i=0;i<count;i++)bytes[i]=Convert.ToByte(source.GetValue(i+lower));
  return count;
 }
 static int OpenHistory(object link,string from,out int files,out int downloads,out string timestamp) {
  object[] values={"RCVN",from,2,0,0,""};
  var byRef=new ParameterModifier(6);byRef[3]=true;byRef[4]=true;byRef[5]=true;
  int code=Convert.ToInt32(link.GetType().InvokeMember("JVOpen",BindingFlags.InvokeMethod,null,link,values,new[]{byRef},CultureInfo.InvariantCulture,null));
  files=Convert.ToInt32(values[3]);downloads=Convert.ToInt32(values[4]);timestamp=Convert.ToString(values[5]);return code;
 }
 static void ReadHistory(object link,string from,string raceKey,string[] horseIds) {
  var wanted=new HashSet<string>(horseIds);var counts=new Dictionary<string,int>();
  var timer=Stopwatch.StartNew();int kept=0,scanned=0,boundaries=0;long scannedBytes=0,keptBytes=0;
  while(true) {
   if(timer.ElapsedMilliseconds>180000)throw new TimeoutException("History read deadline exceeded.");
   byte[] bytes;int code=ReadBytes(link,out bytes);
   if(code==0)break;
   if(code==-1){if(++boundaries>4096)throw new InvalidOperationException("Too many file boundaries.");continue;}
   if(code==-3){int status=Convert.ToInt32(Call(link,"JVStatus"));if(status<0){Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="error",stage="JVStatus",jvCode=status}));return;}Thread.Sleep(100);continue;}
   if(code<0){Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="error",stage="JVGets",jvCode=code}));return;}
   scanned++;scannedBytes+=code;
   if(scanned>150000||scannedBytes>67108864)throw new InvalidOperationException("History scan limit exceeded.");
   string type=bytes.Length>=2?System.Text.Encoding.ASCII.GetString(bytes,0,2):"??";
   // Keep all race details to join with matching SE rows regardless of stream
   // order. Masters, odds and payouts are counted in the scan but never emitted.
   bool retain=type=="RA"||(type=="SE"&&bytes.Length>=40&&wanted.Contains(System.Text.Encoding.ASCII.GetString(bytes,30,10)));
   if(!retain)continue;
   kept++;keptBytes+=code;if(kept>15000||keptBytes>12582912)throw new InvalidOperationException("Retained history limit exceeded.");
   if(!counts.ContainsKey(type))counts[type]=0;counts[type]++;
   Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="record",sequence=kept,recordType=type,byteCount=code,observedAt=DateTimeOffset.UtcNow.ToString("o"),dataBase64=Convert.ToBase64String(bytes)}));
  }
  Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="summary",operation="history",dataSpec="RCVN",option=2,fromTime=from,requestDate=raceKey.Substring(0,8),requestRaceKey=raceKey,targetHorseIds=horseIds,recordsRead=kept,recordsScanned=scanned,scannedBytes=scannedBytes,recordTypes=counts,readComplete=true,forecastAllowed=false}));
 }
 static void ReadRecords(object link,string requestKey,string spec) {
  string day=requestKey.Substring(0,8);
  var counts=new Dictionary<string,int>();
  var timer=Stopwatch.StartNew();int total=0, boundaries=0;
  while(true) {
   if(timer.ElapsedMilliseconds>35000)throw new TimeoutException("Record read deadline exceeded.");
   byte[] bytes;int code=ReadBytes(link,out bytes);
   if(code==0)break;
   if(code==-1){if(++boundaries>4096)throw new InvalidOperationException("Too many file boundaries.");continue;}
   if(code<0) {
    Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="error",stage="JVGets",jvCode=code,recordsRead=total}));
    throw new InvalidOperationException("JVGets failed.");
   }
   if(++total>(spec=="0B30"?32:2000))throw new InvalidOperationException("Record limit exceeded.");
   string type=bytes.Length>=2 ? System.Text.Encoding.ASCII.GetString(bytes,0,2) : "??";
   if(!counts.ContainsKey(type))counts[type]=0;counts[type]++;
   Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="record",sequence=total,recordType=type,byteCount=code,observedAt=DateTimeOffset.UtcNow.ToString("o"),dataBase64=Convert.ToBase64String(bytes)}));
  }
  Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="summary",operation="read",dataSpec=spec,requestDate=day,requestRaceKey=spec=="0B30"?requestKey:null,readComplete=true,recordsRead=total,recordTypes=counts,decoderReady=false,forecastAllowed=false}));
 }
 [STAThread]
 public static int Main(string[] args) {
  object link = null;
  try {
   if(args.Length == 1 && args[0] == "--probe") {
    Console.WriteLine(Json.Serialize(new {protocolVersion=1, operation="probe", networkUsed=false, processBits=IntPtr.Size*8, registered32=Registered(RegistryView.Registry32), registered64=Registered(RegistryView.Registry64), authenticationVerified=false, decoderReady=false}));
    return 0;
   }
   if(args.Length==5&&args[0]=="--history"&&args[1]=="--allow-network"){
    DateTime from;string raceKey=args[3];string[] horseIds=args[4].Split(',');
    if((args[2]!="00000000000000"&&!DateTime.TryParseExact(args[2],"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out from))||!System.Text.RegularExpressions.Regex.IsMatch(raceKey,@"^\d{8}(0[1-9]|10)\d{4}(0[1-9]|1[0-2])$")||horseIds.Length<1||horseIds.Length>18)throw new ArgumentException("Invalid history request.");
    foreach(string id in horseIds)if(!System.Text.RegularExpressions.Regex.IsMatch(id,@"^\d{10}$"))throw new ArgumentException("Invalid horse ID.");
    Type historyType=Type.GetTypeFromProgID(ProgId,false);if(historyType==null)throw new InvalidOperationException("JV-Link is not registered.");
    Console.Error.WriteLine("stage=before-COM");Console.Error.Flush();link=Activator.CreateInstance(historyType);Console.Error.WriteLine("stage=before-JVInit");Console.Error.Flush();int historyInit=Convert.ToInt32(Call(link,"JVInit","UNKNOWN"));Console.Error.WriteLine("stage=after-JVInit");Console.Error.Flush();
    if(historyInit!=0){Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="error",stage="JVInit",jvCode=historyInit}));return 2;}
    Console.Error.WriteLine("stage=before-JVOpen");Console.Error.Flush();int files,downloads;string timestamp;int historyOpen=OpenHistory(link,args[2],out files,out downloads,out timestamp);Console.Error.WriteLine("stage=after-JVOpen");Console.Error.Flush();
    Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="open",operation="history",dataSpec="RCVN",option=2,fromTime=args[2],requestDate=raceKey.Substring(0,8),requestRaceKey=raceKey,targetHorseIds=horseIds,processBits=IntPtr.Size*8,jvInit=historyInit,jvOpen=historyOpen,readFiles=files,downloadFiles=downloads,lastFileTimestamp=timestamp,connectionVerified=historyOpen==0,networkUsed=true,forecastAllowed=false}));
    if(historyOpen!=0)return 2;
    if(files<0||files>128||downloads<0||downloads>files)throw new InvalidOperationException("History file limit exceeded.");
    ReadHistory(link,args[2],raceKey,horseIds);return 0;
   }
   // This explicit command is run by the user only after installation and service
   // approval. Opening 0B15 may authenticate, start a trial and download data.
   bool odds=args.Length==3&&args[0]=="--odds";
   bool changes=args.Length==3&&args[0]=="--changes";
   bool reading=args.Length==3&&(args[0]=="--read"||odds||changes);
   if(args.Length != 3 || (args[0] != "--connect"&&!reading) || args[1] != "--allow-network") throw new ArgumentException("Use --probe, --connect, or --read with --allow-network YYYYMMDD after approving official Data Lab. access.");
   DateTime day;
   string requestKey=args[2],spec=odds?"0B30":changes?"0B14":"0B15";
   if(odds&&!System.Text.RegularExpressions.Regex.IsMatch(requestKey,@"^\d{8}(0[1-9]|10)\d{4}(0[1-9]|1[0-2])$"))throw new ArgumentException("Expected official 16 digit race key.");
   if(!DateTime.TryParseExact(odds?requestKey.Substring(0,8):requestKey,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)) throw new ArgumentException("Expected YYYYMMDD.");
   Type type = Type.GetTypeFromProgID(ProgId, false);
   if(type == null) throw new InvalidOperationException("JV-Link is not registered for this process architecture. Use the matching official SDK verification tool.");
   link = Activator.CreateInstance(type);
   int init = Convert.ToInt32(Call(link,"JVInit","UNKNOWN"));
   if(init != 0) throw new InvalidOperationException("JVInit returned " + init);
   int open = Convert.ToInt32(Call(link,"JVRTOpen",spec,requestKey));
   if(reading) {
    Console.WriteLine(Json.Serialize(new {protocolVersion=1,kind="open",operation="read",dataSpec=spec,requestDate=day.ToString("yyyyMMdd"),requestRaceKey=odds?requestKey:null,networkUsed=true,processBits=IntPtr.Size*8,jvInit=init,jvRtOpen=open,connectionVerified=open==0,decoderReady=false}));
    if(open!=0)return 2;
    ReadRecords(link,requestKey,spec);
    return 0;
   }
   Console.WriteLine(Json.Serialize(new {protocolVersion=1,operation="connect",networkUsed=true,processBits=IntPtr.Size*8,jvInit=init,jvRtOpen=open,connectionVerified=open==0,decoderReady=false,notice="Connection test only. Race decoding is not implemented. No records or credentials are printed."}));
   return open==0 ? 0 : 2;
  } catch(Exception ex) {
   // Do not log COM messages which might contain user-specific settings.
   while(ex is TargetInvocationException && ex.InnerException != null) ex=ex.InnerException;
   Console.WriteLine(Json.Serialize(new {protocolVersion=1,error="JV-Link diagnostic failed",errorType=ex.GetType().Name,hresult=ex.HResult,connectionVerified=false,decoderReady=false}));
   return 1;
  } finally {
   if(link != null) {
    try { Call(link,"JVClose"); } catch { }
    if(Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
   }
  }
 }
}
