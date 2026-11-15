using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotchFlow.Services;
public sealed record UpdateProgress(long Received,long Total){public double Percent=>100d*Received/Total;}
public sealed class UpdateService : IDisposable
{
#if LOCAL_UPDATE_TEST
 public static readonly Uri ManifestUri=new("http://127.0.0.1:8765/notchflow/version.json");
#else
 public static readonly Uri ManifestUri=new("https://195.78.49.209:8443/notchflow/version.json");
#endif
 readonly HttpClient http;
 readonly bool ownsClient;
 readonly Uri endpoint;
 readonly UpdateVerifier verifier;
 readonly string directory;
 readonly SemaphoreSlim downloadGate=new(1,1);
 public Version CurrentVersion{get;}
 public UpdateService():this(CreateClient(),ReadPublicKey(),ManifestUri,Path.Combine(Log.DirectoryPath,"Updates"),typeof(UpdateService).Assembly.GetName().Version!,true){}
 public UpdateService(HttpClient http,string publicKey,Uri endpoint,string directory,Version currentVersion,bool ownsClient=false)
 {
  if(endpoint.Scheme!="https"&&!(endpoint.Scheme=="http"&&endpoint.IsLoopback))throw new ArgumentException("Updates require HTTPS.");
  this.http=http;this.endpoint=endpoint;this.directory=directory;this.ownsClient=ownsClient;verifier=new UpdateVerifier(publicKey,endpoint);
  CurrentVersion=new Version(currentVersion.Major,currentVersion.Minor,Math.Max(0,currentVersion.Build),Math.Max(0,currentVersion.Revision));
 }
 public static HttpClient CreateClient()=>new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
 static string ReadPublicKey(){using var stream=typeof(UpdateService).Assembly.GetManifestResourceStream("NotchFlow.UpdatePublicKey.pem")??throw new InvalidOperationException("Не найден ключ проверки обновлений.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
 async Task<HttpResponseMessage> GetAsync(Uri uri,CancellationToken token)
 {
  var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
  if((int)response.StatusCode is >=300 and <400){response.Dispose();throw new UpdateSecurityException("Перенаправление загрузки запрещено.");}
  if(response.RequestMessage?.RequestUri!=uri){response.Dispose();throw new UpdateSecurityException("Адрес ответа не совпадает с запросом.");}
  if(!response.IsSuccessStatusCode){var status=response.StatusCode;response.Dispose();throw new HttpRequestException("Сервер обновлений временно недоступен.",null,status);}
  return response;
 }
 async Task<byte[]> ReadSmallAsync(Uri uri,int maximum,CancellationToken token)
 {
  using var response=await GetAsync(uri,token);
  if(response.Content.Headers.ContentLength>maximum)throw new UpdateSecurityException("Ответ сервера слишком большой.");
  await using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[4096];
  int count;while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>maximum)throw new UpdateSecurityException("Ответ сервера слишком большой.");output.Write(buffer,0,count);}return output.ToArray();
 }
 public async Task<VerifiedUpdate?> CheckAsync(CancellationToken token)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(40));
  // Download and authenticate raw bytes before deserializing any fields.
  var bytes=await ReadSmallAsync(endpoint,65536,deadline.Token);
  byte[] signature;
  try{signature=Convert.FromBase64String(Encoding.ASCII.GetString(await ReadSmallAsync(new Uri(endpoint.AbsoluteUri+".sig"),2048,deadline.Token)));}
  catch(Exception ex) when(ex is FormatException||ex is HttpRequestException {StatusCode:System.Net.HttpStatusCode.NotFound}){throw new UpdateSecurityException("Подпись обновления отсутствует или повреждена. Установка запрещена.");}
  var release=verifier.Verify(bytes,signature);
  return UpdateVerifier.ParseVersion(release.Manifest.Version)>CurrentVersion?release:null;
 }
 string Target(VerifiedUpdate release)=>Path.Combine(directory,$"NotchFlow-{release.Manifest.Version}-{release.Manifest.Sha256[..16].ToLowerInvariant()}-Setup.exe");
 public async Task<string> DownloadAsync(VerifiedUpdate release,IProgress<UpdateProgress>? progress,CancellationToken token)
 {
  if(!await downloadGate.WaitAsync(0,token))throw new InvalidOperationException("Загрузка уже выполняется.");
  string? part=null;
  try
  {
   release=verifier.Verify(release.Bytes,release.Signature);
   if(UpdateVerifier.ParseVersion(release.Manifest.Version)<=CurrentVersion)throw new UpdateSecurityException("Установка более старой версии запрещена.");
   Directory.CreateDirectory(directory);var target=Target(release);
   using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(15));var ct=deadline.Token;
   if(File.Exists(target))
   {
    try{using var cached=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read);await UpdateVerifier.VerifyFileAsync(cached,release.Manifest,ct);progress?.Report(new(release.Manifest.SizeBytes,release.Manifest.SizeBytes));return target;}
    catch(UpdateSecurityException){File.Delete(target);}
   }
   using var response=await GetAsync(verifier.ValidateDownloadUri(release.Manifest.DownloadUrl),ct);
   if(response.Content.Headers.ContentLength is long length&&length!=release.Manifest.SizeBytes)throw new UpdateSecurityException("Сервер вернул неверный размер установщика.");
   part=target+"."+Guid.NewGuid().ToString("N")+".part";
   await using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,81920,true))
   {
    await using var input=await response.Content.ReadAsStreamAsync(ct);var buffer=new byte[81920];long total=0;int read;
    while((read=await input.ReadAsync(buffer,ct))>0)
    {
     total+=read;if(total>release.Manifest.SizeBytes)throw new UpdateSecurityException("Загрузка превысила допустимый размер.");
     await output.WriteAsync(buffer.AsMemory(0,read),ct);progress?.Report(new(total,release.Manifest.SizeBytes));
    }
    await output.FlushAsync(ct);await UpdateVerifier.VerifyFileAsync(output,release.Manifest,ct);
   }
   File.Move(part,target,true);part=null;return target;
  }
  finally{try{if(part!=null&&File.Exists(part))File.Delete(part);}finally{downloadGate.Release();}}
 }
 // Keep a read lock through Process.Start, so a downloaded executable cannot change during launch verification.
 public async Task<FileStream> OpenVerifiedInstallerAsync(VerifiedUpdate release,string path,CancellationToken token)
 {
  release=verifier.Verify(release.Bytes,release.Signature);
  if(UpdateVerifier.ParseVersion(release.Manifest.Version)<=CurrentVersion||!string.Equals(Path.GetFullPath(path),Path.GetFullPath(Target(release)),StringComparison.OrdinalIgnoreCase))throw new UpdateSecurityException("Неверный файл установщика.");
  var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
  try{await UpdateVerifier.VerifyFileAsync(file,release.Manifest,token);return file;}catch{file.Dispose();throw;}
 }
 public void Dispose(){if(ownsClient)http.Dispose();}
}
