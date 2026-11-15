using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NotchFlow.Services;

public sealed record UpdateManifest(string Version,string Channel,string Platform,string DownloadUrl,string Sha256,long SizeBytes,string ReleaseNotes);
public sealed class VerifiedUpdate
{
 internal VerifiedUpdate(UpdateManifest manifest,byte[] bytes,byte[] signature){Manifest=manifest;Bytes=(byte[])bytes.Clone();Signature=(byte[])signature.Clone();}
 public UpdateManifest Manifest{get;}
 internal byte[] Bytes{get;}
 internal byte[] Signature{get;}
}
public sealed class UpdateSecurityException : Exception
{
 public UpdateSecurityException(string message):base(message){}
}
public sealed class UpdateVerifier
{
 public const long MaximumInstallerSize=512L*1024*1024;
 readonly string publicKey;
 readonly Uri origin;
 public UpdateVerifier(string publicKey,Uri origin){this.publicKey=publicKey;this.origin=origin;}
 public static Version ParseVersion(string value)
 {
  if(value==null||!Regex.IsMatch(value,@"\A\d{1,6}\.\d{1,6}\.\d{1,6}(\.\d{1,6})?\z")||!Version.TryParse(value,out var version))throw new UpdateSecurityException("Неверный номер версии обновления.");
  return new Version(version.Major,version.Minor,version.Build,Math.Max(0,version.Revision));
 }
 public VerifiedUpdate Verify(byte[] bytes,byte[] signature)
 {
  if(bytes.Length==0||bytes.Length>65536||signature.Length>1024)throw new UpdateSecurityException("Недопустимый размер описания обновления.");
  using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
  if(!rsa.VerifyData(bytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new UpdateSecurityException("Подпись обновления неверна. Установка запрещена.");
  UpdateManifest m;
  try{m=JsonSerializer.Deserialize<UpdateManifest>(bytes,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})??throw new JsonException();}
  catch(JsonException){throw new UpdateSecurityException("Описание обновления повреждено.");}
  ParseVersion(m.Version);
  if(m.Channel!="stable"||m.Platform!="win-x64")throw new UpdateSecurityException("Обновление не подходит для этой платформы или канала.");
  if(m.SizeBytes<=0||m.SizeBytes>MaximumInstallerSize||m.Sha256==null||!Regex.IsMatch(m.Sha256,@"\A[0-9a-fA-F]{64}\z"))throw new UpdateSecurityException("Неверные данные проверки установщика.");
  ValidateDownloadUri(m.DownloadUrl);
  if(m.ReleaseNotes==null||m.ReleaseNotes.Length>16000)throw new UpdateSecurityException("Некорректное описание изменений.");
  return new VerifiedUpdate(m,bytes,signature);
 }
 public Uri ValidateDownloadUri(string value)
 {
  if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!=origin.Scheme||uri.Host!=origin.Host||uri.Port!=origin.Port||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||!Regex.IsMatch(uri.AbsolutePath,@"\A/notchflow/releases/[A-Za-z0-9][A-Za-z0-9._-]*\.exe\z"))throw new UpdateSecurityException("Небезопасный адрес загрузки. Обновление отменено.");
  return uri;
 }
 public static async Task VerifyFileAsync(Stream stream,UpdateManifest manifest,CancellationToken token)
 {
  if(stream.Length!=manifest.SizeBytes)throw new UpdateSecurityException("Размер установщика не совпадает. Файл не будет запущен.");
  stream.Position=0;var actual=await SHA256.HashDataAsync(stream,token);
  if(!CryptographicOperations.FixedTimeEquals(actual,Convert.FromHexString(manifest.Sha256)))throw new UpdateSecurityException("Установщик повреждён или подменён. Файл не будет запущен.");
  stream.Position=0;
 }
}
