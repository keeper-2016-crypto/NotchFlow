param([string]$PackageDirectory=(Join-Path $PSScriptRoot 'server/notchflow'))
$ErrorActionPreference='Stop'
$base=[IO.Path]::GetFullPath($PackageDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $base version.json) -Raw | ConvertFrom-Json
$uri=[Uri]$manifest.downloadUrl
if($uri.Scheme -ne 'http' -or $uri.Host -ne '127.0.0.1' -or $uri.Port -ne 8765){throw 'This server accepts only the local test package.'}
$routes=@{'/notchflow/version.json'=(Join-Path $base version.json);'/notchflow/version.json.sig'=(Join-Path $base version.json.sig)}
$routes[$uri.AbsolutePath]=Join-Path $base ('releases/'+[IO.Path]::GetFileName($uri.AbsolutePath))
foreach($file in $routes.Values){if(!(Test-Path -LiteralPath $file)){throw "Missing test file: $file"}}
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,8765)
$listener.Start()
Write-Host 'Local test server: http://127.0.0.1:8765/notchflow/version.json — Ctrl+C to stop.'
try {
 while($true){
  $client=$listener.AcceptTcpClient()
  try {
   $stream=$client.GetStream();$stream.ReadTimeout=10000;$stream.WriteTimeout=30000
   $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::ASCII,$false,1024,$true)
   $request=$reader.ReadLine();if(!$request){continue}
   while($true){$line=$reader.ReadLine();if([string]::IsNullOrEmpty($line)){break}}
   $parts=$request.Split(' ');$path=$parts[1]
   if($parts[0] -eq 'GET' -and $routes.ContainsKey($path)){
    $file=[IO.File]::OpenRead($routes[$path])
    try{$header=[Text.Encoding]::ASCII.GetBytes("HTTP/1.1 200 OK`r`nContent-Type: application/octet-stream`r`nContent-Length: $($file.Length)`r`nConnection: close`r`n`r`n");$stream.Write($header,0,$header.Length);$file.CopyTo($stream)}finally{$file.Dispose()}
   }else{$header=[Text.Encoding]::ASCII.GetBytes("HTTP/1.1 404 Not Found`r`nContent-Length: 0`r`nConnection: close`r`n`r`n");$stream.Write($header,0,$header.Length)}
  }catch{Write-Warning 'Request interrupted.'}finally{$client.Dispose()}
 }
}finally{$listener.Stop()}
