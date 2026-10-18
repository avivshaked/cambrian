# Minimal Chrome DevTools Protocol driver for Edge, using only .NET's ClientWebSocket. Dot-source it.
# Everything it writes goes under D:\Projects\experiments\evolution-simulator\scratch\room6-inspect\.
$script:Root = 'D:\Projects\experiments\evolution-simulator\scratch\room6-inspect'
$script:Base = 'http://127.0.0.1:5179'
$script:Ws = $null; $script:Id = 0; $script:RecvTask = $null; $script:Buf = New-Object byte[] 4194304
$script:Console = New-Object System.Collections.ArrayList
$script:Exceptions = New-Object System.Collections.ArrayList
$script:Responses = @{}   # requestId -> {url,status,type}
$script:Failed = New-Object System.Collections.ArrayList
$script:Bad = New-Object System.Collections.ArrayList
$script:EdgePid = $null
$script:Reqs = New-Object System.Collections.ArrayList

function Start-Edge([int]$w = 1920, [int]$h = 1080) {
  $exe = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
  $p = Start-Process -FilePath $exe -PassThru -ArgumentList @('--headless=new', '--remote-debugging-port=9333', "--user-data-dir=$script:Root\edge", "--window-size=$w,$h", '--no-first-run', '--disable-gpu', 'about:blank')
  $script:EdgePid = $p.Id
  Set-Content -Path "$script:Root\edge.pid" -Value $p.Id
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    try { $t = Invoke-RestMethod 'http://127.0.0.1:9333/json' -TimeoutSec 2; $pg = $t | Where-Object { $_.type -eq 'page' } | Select-Object -First 1; if ($pg) { break } } catch {}
  }
  if (-not $pg) { throw 'edge page not found' }
  $script:Ws = [System.Net.WebSockets.ClientWebSocket]::new()
  $script:Ws.ConnectAsync([uri]$pg.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait()
  Send 'Page.enable' | Out-Null; Send 'Runtime.enable' | Out-Null; Send 'Network.enable' | Out-Null; Send 'Log.enable' | Out-Null
  Set-Viewport $w $h
}

function Stop-Edge {
  try { if ($script:Ws) { $script:Ws.Abort() } } catch {}
  if ($script:EdgePid) {
    # children of our edge (renderers, gpu) carry our user-data-dir in their command line
    $kids = Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" | Where-Object { $_.CommandLine -like "*room6-inspect*edge*" }
    foreach ($k in $kids) { Stop-Process -Id $k.ProcessId -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $script:EdgePid -Force -ErrorAction SilentlyContinue
  }
}

function Set-Viewport([int]$w, [int]$h) {
  Send 'Emulation.setDeviceMetricsOverride' @{ width = $w; height = $h; deviceScaleFactor = 1; mobile = $false } | Out-Null
}

function Handle-Event($m) {
  switch ($m.method) {
    'Runtime.consoleAPICalled' {
      $txt = ($m.params.args | ForEach-Object { if ($null -ne $_.value) { "$($_.value)" } elseif ($_.description) { $_.description } else { $_.type } }) -join ' '
      if ($m.params.type -in 'error', 'warning', 'assert') { [void]$script:Console.Add([pscustomobject]@{ t = (Get-Date).ToString('HH:mm:ss'); type = $m.params.type; text = $txt }) }
    }
    'Runtime.exceptionThrown' { $d = $m.params.exceptionDetails; [void]$script:Exceptions.Add([pscustomobject]@{ t = (Get-Date).ToString('HH:mm:ss'); text = $d.text; desc = $d.exception.description; url = $d.url; line = $d.lineNumber; col = $d.columnNumber }) }
    'Log.entryAdded' { $e = $m.params.entry; if ($e.level -in 'error', 'warning') { [void]$script:Console.Add([pscustomobject]@{ t = (Get-Date).ToString('HH:mm:ss'); type = 'log.' + $e.level; text = "$($e.text) $($e.url)" }) } }
    'Network.requestWillBeSent' { [void]$script:Reqs.Add("$($m.params.request.method) $($m.params.request.url)") }
    'Network.responseReceived' {
      $r = $m.params.response; $script:Responses[$m.params.requestId] = [pscustomobject]@{ url = $r.url; status = $r.status }
      if ($r.status -ge 400) { [void]$script:Bad.Add([pscustomobject]@{ id = $m.params.requestId; url = $r.url; status = $r.status; body = $null }) }
    }
    'Network.loadingFailed' { $u = $script:Responses[$m.params.requestId]; [void]$script:Failed.Add([pscustomobject]@{ id = $m.params.requestId; err = $m.params.errorText; canceled = $m.params.canceled; url = if ($u) { $u.url } else { '?' } }) }
  }
}

function Pump([int]$ms) {
  # reads whatever arrives for $ms milliseconds; returns the first message that has an id if waitId is set via script var
  $end = [DateTime]::UtcNow.AddMilliseconds($ms)
  while ([DateTime]::UtcNow -lt $end) {
    if (-not $script:RecvTask) { $script:RecvTask = $script:Ws.ReceiveAsync([ArraySegment[byte]]::new($script:Buf), [Threading.CancellationToken]::None) }
    $left = [int]($end - [DateTime]::UtcNow).TotalMilliseconds
    if ($left -le 0) { break }
    if (-not $script:RecvTask.Wait($left)) { break }
    $res = $script:RecvTask.Result; $script:RecvTask = $null
    $len = $res.Count
    while (-not $res.EndOfMessage) {
      $seg = [ArraySegment[byte]]::new($script:Buf, $len, $script:Buf.Length - $len)
      $res = $script:Ws.ReceiveAsync($seg, [Threading.CancellationToken]::None).Result; $len += $res.Count
    }
    $json = [Text.Encoding]::UTF8.GetString($script:Buf, 0, $len)
    $m = $json | ConvertFrom-Json -Depth 50
    if ($m.id -ne $null) { $script:Replies[[int]$m.id] = $m } else { Handle-Event $m }
  }
}
$script:Replies = @{}

function Send([string]$method, $params = @{}) {
  $script:Id++; $id = $script:Id
  $msg = @{ id = $id; method = $method; params = $params } | ConvertTo-Json -Depth 30 -Compress
  $bytes = [Text.Encoding]::UTF8.GetBytes($msg)
  $script:Ws.SendAsync([ArraySegment[byte]]::new($bytes), 'Text', $true, [Threading.CancellationToken]::None).Wait()
  $deadline = [DateTime]::UtcNow.AddSeconds(60)
  while (-not $script:Replies.ContainsKey($id)) {
    if ([DateTime]::UtcNow -gt $deadline) { throw "timeout waiting for $method" }
    Pump 300
  }
  $r = $script:Replies[$id]; $script:Replies.Remove($id)
  if ($r.error) { throw "$method error: $($r.error.message)" }
  return $r.result
}

function Wait-Ms([int]$ms) { Pump $ms }

function Nav([string]$url) {
  Send 'Page.navigate' @{ url = $url } | Out-Null
  Wait-Ms 1500
}

# Evaluates a JS expression (may return a promise); returns the value (objects come back as JSON-parsed).
function Ev([string]$js) {
  $r = Send 'Runtime.evaluate' @{ expression = $js; awaitPromise = $true; returnByValue = $true; timeout = 30000 }
  if ($r.exceptionDetails) { return "JSERR: $($r.exceptionDetails.exception.description)" }
  return $r.result.value
}

function Shot([string]$name) {
  $r = Send 'Page.captureScreenshot' @{ format = 'png' }
  $path = "$script:Root\shots\$name.png"
  [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($r.data))
  return $path
}

function Click-At([double]$x, [double]$y) {
  Send 'Input.dispatchMouseEvent' @{ type = 'mouseMoved'; x = $x; y = $y } | Out-Null
  Send 'Input.dispatchMouseEvent' @{ type = 'mousePressed'; x = $x; y = $y; button = 'left'; clickCount = 1 } | Out-Null
  Send 'Input.dispatchMouseEvent' @{ type = 'mouseReleased'; x = $x; y = $y; button = 'left'; clickCount = 1 } | Out-Null
}

# Real click on the center of the first element matching selector (after scrolling it into view).
function Click-Sel([string]$sel) {
  $pos = Ev "(()=>{const e=document.querySelector($(ConvertTo-Json $sel));if(!e)return null;e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return JSON.stringify({x:r.x+r.width/2,y:r.y+r.height/2,w:r.width,h:r.height})})()"
  if (-not $pos) { return $false }
  $p = $pos | ConvertFrom-Json
  Click-At $p.x $p.y
  return $true
}

function Drain-Bodies {
  foreach ($b in $script:Bad) { if ($null -eq $b.body) { try { $r = Send 'Network.getResponseBody' @{ requestId = $b.id }; $b.body = $r.body } catch { $b.body = '(body unavailable)' } } }
}

function Clear-Collect { $script:Console.Clear(); $script:Exceptions.Clear(); $script:Failed.Clear(); $script:Bad.Clear() }

function Report-Collect([string]$label) {
  Drain-Bodies
  "---- $label : console errors/warnings: $($script:Console.Count), exceptions: $($script:Exceptions.Count), failed: $($script:Failed.Count), 4xx/5xx: $($script:Bad.Count)"
  foreach ($c in $script:Console) { "  console.$($c.type) $($c.t): $($c.text)" }
  foreach ($c in $script:Exceptions) { "  EXC $($c.t): $($c.desc) $($c.text) @ $($c.url):$($c.line):$($c.col)" }
  foreach ($c in $script:Failed) { "  LOADFAIL $($c.url): $($c.err) canceled=$($c.canceled)" }
  foreach ($c in $script:Bad) { "  HTTP $($c.status) $($c.url) body=$(($c.body -replace '\s+',' ').Substring(0,[Math]::Min(300, "$($c.body)".Length)))" }
}

# A DOM audit: text of every panel, with a flag for empty/loading/undefined/NaN/null/[object Object].
$script:AuditJs = @'
(()=>{
  const out = {bad:[], panels:[], domNodes: document.getElementsByTagName('*').length, title: document.title, url: location.href, scrollW: document.documentElement.scrollWidth, scrollH: document.documentElement.scrollHeight, innerW: innerWidth, innerH: innerHeight};
  const re = /\b(undefined|NaN|null|\[object Object\]|loading)\b/i;
  for (const p of document.querySelectorAll('section.panel, aside.panel, nav.panel, header, footer, main > *')) {
    const t = (p.innerText||'').replace(/\s+/g,' ').trim();
    const r = p.getBoundingClientRect();
    out.panels.push({tag:p.tagName.toLowerCase(), aria:p.getAttribute('aria-label')||p.id||'', len:t.length, w:Math.round(r.width), h:Math.round(r.height), head:t.slice(0,90)});
    const m = t.match(re); if (m) out.bad.push({aria:p.getAttribute('aria-label')||p.id, match:m[0], ctx:t.slice(Math.max(0,t.search(re)-40), t.search(re)+60)});
    if (t.length < 12) out.bad.push({aria:p.getAttribute('aria-label')||p.id, match:'EMPTY', ctx:t});
  }
  // overflowing or clipped text
  out.clipped = [...document.querySelectorAll('body *')].filter(e=>e.children.length===0 && e.scrollWidth>e.clientWidth+2 && getComputedStyle(e).overflow==='visible' && e.clientWidth>0).slice(0,10).map(e=>({cls:e.className&&e.className.baseVal!==undefined?e.className.baseVal:e.className, tag:e.tagName, txt:(e.textContent||'').slice(0,50), sw:e.scrollWidth, cw:e.clientWidth}));
  return JSON.stringify(out);
})()
'@
function Audit { (Ev $script:AuditJs) | ConvertFrom-Json }
