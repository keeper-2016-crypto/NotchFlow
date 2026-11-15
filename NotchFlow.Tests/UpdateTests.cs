using System.Net;
using System.Net.Http;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NotchFlow.Services;

internal static class UpdateTests
{
 static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS: "+name);}
 static async Task Reject(Func<Task> action,string name){try{await action();}catch(Exception ex) when(ex is UpdateSecurityException or HttpRequestException or IOException or OperationCanceledException or InvalidOperationException){Console.WriteLine("PASS: "+name);return;}throw new Exception("Expected rejection: "+name);}
 public static async Task Run(string root)
 {
  using var key=RSA.Create(2048);using var other=RSA.Create(2048);await using var server=new Fixture();
  var publicKey=key.ExportSubjectPublicKeyInfoPem();int sequence=0;
  var bytes=Enumerable.Range(0,262144).Select(n=>(byte)(n%251)).ToArray();
  string hash=Convert.ToHexString(SHA256.HashData(bytes));
  UpdateManifest Manifest(string version="0.3.6")=>new(version,"stable","win-x64",server.Base+"/notchflow/releases/NotchFlow-0.3.6-Setup.exe",hash,bytes.Length,"Тестовое обновление");
  byte[] Encode(UpdateManifest m)=>JsonSerializer.SerializeToUtf8Bytes(m,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
  void Set(UpdateManifest? m=null){server.Manifest=Encode(m??Manifest());server.Signature=Encoding.ASCII.GetBytes(Convert.ToBase64String(key.SignData(server.Manifest,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));server.Installer=bytes;server.SignatureStatus=200;server.DownloadStatus=200;server.Truncate=false;server.Slow=false;}
  UpdateService Service()=>new(UpdateService.CreateClient(),publicKey,new Uri(server.Base+"/notchflow/version.json"),Path.Combine(root,"updater-"+sequence++),new Version(0,3,5),true);
  var verifier=new UpdateVerifier(publicKey,new Uri(server.Base));
  Check(UpdateVerifier.ParseVersion("0.3.10")>UpdateVerifier.ParseVersion("0.3.9")&&UpdateVerifier.ParseVersion("1.2.3")==UpdateVerifier.ParseVersion("1.2.3.0"),"UpdateVersionsNumeric");
  foreach(var version in new[]{"0.3.5","0.3.4"}){Set(Manifest(version));using var service=Service();Check(await service.CheckAsync(default)==null,"UpdateNoDowngrade-"+version);}
  Set();using(var service=Service()){Check((await service.CheckAsync(default))?.Manifest.Version=="0.3.6","UpdateNewVersion");}
  Set();server.SignatureStatus=404;using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateMissingSignature");
  Set();server.Signature=Encoding.ASCII.GetBytes("not-base64");using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateMalformedSignature");
  Set();server.SignatureStatus=503;using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateServerHttpFailure");
  Set();server.Signature=Encoding.ASCII.GetBytes(Convert.ToBase64String(other.SignData(server.Manifest,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateWrongSigningKey");
  Set();server.Manifest[0]=(byte)'!';using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateTamperedRawBytes");
  Set();server.Manifest=Encoding.UTF8.GetBytes("{broken");server.Signature=Encoding.ASCII.GetBytes(Convert.ToBase64String(key.SignData(server.Manifest,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateSignedInvalidJson");
  foreach(var m in new[]{Manifest() with {Channel="beta"},Manifest() with {Platform="linux-x64"},Manifest() with {SizeBytes=UpdateVerifier.MaximumInstallerSize+1},Manifest() with {DownloadUrl="https://example.org/evil.exe"},Manifest() with {DownloadUrl=server.Base+"/notchflow/releases/test.exe?token=bad"}})
  {Set(m);using var service=Service();await Reject(async()=>await service.CheckAsync(default),"UpdatePolicyReject-"+sequence);}
  Set();using(var service=Service())
  {
   var release=(await service.CheckAsync(default))!;long progress=0;var file=await service.DownloadAsync(release,new InlineProgress(p=>progress=p.Received),default);
   Check(File.ReadAllBytes(file).SequenceEqual(bytes)&&progress==bytes.Length,"UpdateSignedDownloadAndProgress");
   int requests=server.Downloads;Check(await service.DownloadAsync(release,null,default)==file&&server.Downloads==requests,"UpdateVerifiedCacheReuse");
   File.WriteAllBytes(file,new byte[bytes.Length]);await service.DownloadAsync(release,null,default);
   Check(File.ReadAllBytes(file).SequenceEqual(bytes)&&server.Downloads==requests+1,"UpdateCorruptCacheRedownload");
   File.WriteAllBytes(file,new byte[bytes.Length]);await Reject(async()=>{using var stream=await service.OpenVerifiedInstallerAsync(release,file,default);},"UpdateReverifyBeforeLaunch");
  }
  Set();server.Installer=new byte[bytes.Length];using(var service=Service()){var release=(await service.CheckAsync(default))!;await Reject(async()=>await service.DownloadAsync(release,null,default),"UpdateHashMismatch");}
  Set();server.Installer=new byte[3];using(var service=Service()){var release=(await service.CheckAsync(default))!;await Reject(async()=>await service.DownloadAsync(release,null,default),"UpdateSizeMismatch");}
  Set();server.Truncate=true;using(var service=Service()){var release=(await service.CheckAsync(default))!;await Reject(async()=>await service.DownloadAsync(release,null,default),"UpdateInterruptedDownload");}
  Set();server.DownloadStatus=302;using(var service=Service()){var release=(await service.CheckAsync(default))!;await Reject(async()=>await service.DownloadAsync(release,null,default),"UpdateRedirectRejected");}
  Set();server.Slow=true;using(var service=Service())
  {
   var release=(await service.CheckAsync(default))!;using var cancellation=new CancellationTokenSource();
   var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   var download=service.DownloadAsync(release,new InlineProgress(p=>{if(p.Received>0)started.TrySetResult();}),cancellation.Token);
   await started.Task.WaitAsync(TimeSpan.FromSeconds(5));await Reject(async()=>await service.DownloadAsync(release,null,default),"UpdateDuplicateDownloadBlocked");
   cancellation.Cancel();await Reject(async()=>await download,"UpdateCancelledDownload");
  }
  Check(!Directory.EnumerateFiles(root,"*.part",SearchOption.AllDirectories).Any(),"UpdateFailedDownloadsCleaned");
  Check(!UpdateCoordinator.IsDue(DateTimeOffset.UtcNow.AddHours(-11),DateTimeOffset.UtcNow)&&UpdateCoordinator.IsDue(DateTimeOffset.UtcNow.AddHours(-12),DateTimeOffset.UtcNow),"UpdateTwelveHourLimit");
  Set(Manifest("0.3.5"));var statePath=Path.Combine(root,"update-state.json");
  using(var coordinator=new UpdateCoordinator(Service(),()=>new(),statePath)){await coordinator.CheckAsync(true);Check(coordinator.LastSuccess!="Ещё не было","UpdateSuccessfulCheckRecorded");}
  using(var coordinator=new UpdateCoordinator(Service(),()=>new(),statePath)){Check(coordinator.LastSuccess!="Ещё не было","UpdateCheckStateSurvivesRestart");}
  var signedPackage=Path.Combine(Environment.CurrentDirectory,".build","signed-package-test");
  if(Directory.Exists(signedPackage))
  {
   using var embedded=typeof(UpdateService).Assembly.GetManifestResourceStream("NotchFlow.UpdatePublicKey.pem")!;using var reader=new StreamReader(embedded);
   var verified=new UpdateVerifier(reader.ReadToEnd(),UpdateService.ManifestUri).Verify(File.ReadAllBytes(Path.Combine(signedPackage,"version.json")),Convert.FromBase64String(File.ReadAllText(Path.Combine(signedPackage,"version.json.sig"))));
   Check(verified.Manifest.Version=="99.0.0","UpdateSigningToolMatchesEmbeddedKey");
  }
  await server.StopAsync();using(var service=Service())await Reject(async()=>await service.CheckAsync(default),"UpdateServerUnavailable");
 }
 sealed class InlineProgress(Action<UpdateProgress> report):IProgress<UpdateProgress>{public void Report(UpdateProgress value)=>report(value);}
 sealed class Fixture:IAsyncDisposable
 {
  readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly Task loop;
  public string Base{get;}
  public byte[] Manifest=[],Signature=[],Installer=[];public int SignatureStatus=200,DownloadStatus=200,Downloads;public bool Truncate,Slow;
  public Fixture(){listener.Start();Base="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;loop=Run();}
  async Task Run(){try{while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);_ = Serve(client);}}catch(Exception ex) when(ex is OperationCanceledException or SocketException){} }
  async Task Serve(TcpClient client)
  {
   using(client)try
   {
    var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);var line=await reader.ReadLineAsync(stop.Token);if(line==null)return;
    string path=line.Split(' ')[1];while(!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))){}
    bool download=path.Contains("/releases/");if(download)Interlocked.Increment(ref Downloads);
    var body=download?Installer:path.EndsWith(".sig")?Signature:Manifest;int code=download?DownloadStatus:path.EndsWith(".sig")?SignatureStatus:200;
    var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 {code} Test\r\nContent-Length: {body.Length}\r\nConnection: close\r\n"+(code==302?"Location: https://example.org/evil.exe\r\n":"")+"\r\n");await stream.WriteAsync(headers,stop.Token);
    int limit=download&&Truncate?body.Length/2:body.Length;
    for(int i=0;i<limit;i+=4096){await stream.WriteAsync(body.AsMemory(i,Math.Min(4096,limit-i)),stop.Token);if(download&&Slow)await Task.Delay(30,stop.Token);}
   }catch(Exception ex) when(ex is IOException or OperationCanceledException or SocketException){}
  }
  public async Task StopAsync(){stop.Cancel();listener.Stop();await loop;}
  public async ValueTask DisposeAsync()=>await StopAsync();
 }
}
