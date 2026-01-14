# EDR Parser Implementation

## Plugin: Defender

`DefenderEdrPlugin` wil grab the events from windows events. 

It will look like this (beautified XML):
```
<Event
	xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>
	<System>
		<Provider Name='Microsoft-Windows-Windows Defender' Guid='{11cd958a-c507-4ef3-b3f2-5fd9dfbd2c78}'/>
		<EventID>1116</EventID>
		<Version>0</Version>
		<Level>3</Level>
		<Task>0</Task>
		<Opcode>0</Opcode>
		<Keywords>0x8000000000000000</Keywords>
		<TimeCreated SystemTime='2025-07-04T14:55:39.8297448Z'/>
		<EventRecordID>39008</EventRecordID>
		<Correlation/>
		<Execution ProcessID='3196' ThreadID='8072'/>
		<Channel>Microsoft-Windows-Windows Defender/Operational</Channel>
		<Computer>DESKTOP-6ENUR41</Computer>
		<Security UserID='S-1-5-18'/>
	</System>
	<EventData>
		<Data Name='Product Name'>Microsoft Defender Antivirus</Data>
		<Data Name='Product Version'>4.18.25050.5</Data>
		<Data Name='Detection ID'>{3DC200B4-DC42-44EC-8B0C-8F88840A56A2}</Data>
		<Data Name='Detection Time'>2025-07-04T14:55:39.823Z</Data>
		<Data Name='Unused'></Data>
		<Data Name='Unused2'></Data>
		<Data Name='Threat ID'>2147728104</Data>
		<Data Name='Threat Name'>Behavior:Win32/Meterpreter.gen!D</Data>
		<Data Name='Severity ID'>5</Data>
		<Data Name='Severity Name'>Severe</Data>
		<Data Name='Category ID'>46</Data>
		<Data Name='Category Name'>Suspicious Behaviour</Data>
		<Data Name='FWLink'>https://go.microsoft.com/fwlink/?linkid=37020&amp;name=Behavior:Win32/Meterpreter.gen!D&amp;threatid=2147728104&amp;enterprise=0</Data>
		<Data Name='Status Code'>1</Data>
		<Data Name='Status Description'></Data>
		<Data Name='State'>1</Data>
		<Data Name='Source ID'>2</Data>
		<Data Name='Source Name'>System</Data>
		<Data Name='Process Name'>Unknown</Data>
		<Data Name='Detection User'>NT AUTHORITY\SYSTEM</Data>
		<Data Name='Unused3'></Data>
		<Data Name='Path'>behavior:_process: C:\NotWhitelisted\ShellcodeGuard-shc.exe, pid:11820:56844127554067; file:_C:\NotWhitelisted\ShellcodeGuard-shc.exe</Data>
		<Data Name='Origin ID'>1</Data>
		<Data Name='Origin Name'>Local machine</Data>
		<Data Name='Execution ID'>0</Data>
		<Data Name='Execution Name'>Unknown</Data>
		<Data Name='Type ID'>2</Data>
		<Data Name='Type Name'>Generic</Data>
		<Data Name='Pre Execution Status'>0</Data>
		<Data Name='Action ID'>9</Data>
		<Data Name='Action Name'>Not Applicable</Data>
		<Data Name='Unused4'></Data>
		<Data Name='Error Code'>0x00000000</Data>
		<Data Name='Error Description'>The operation completed successfully. </Data>
		<Data Name='Unused5'></Data>
		<Data Name='Post Clean Status'>0</Data>
		<Data Name='Additional Actions ID'>0</Data>
		<Data Name='Additional Actions String'>No additional actions required</Data>
		<Data Name='Remediation User'></Data>
		<Data Name='Unused6'></Data>
		<Data Name='Security intelligence Version'>AV: 1.431.401.0, AS: 1.431.401.0, NIS: 1.431.401.0</Data>
		<Data Name='Engine Version'>AM: 1.1.25050.6, NIS: 1.1.25050.6</Data>
	</EventData>
```

DetonatorAgent will attempt to parse, and generate: 

{
  "success": true,
  "alerts": [
    {
      "source": "Defender Local",      
      "alertId": "{3F8AE8C6-70BF-4781-BD6C-2E9C0E996F1D}",
      "title": "HackTool:Win32/Mimikatz!pz",
      "severity": "High",
      "category": "Tool",
      "detectionSource": "Real-Time Protection",
      "detectedAt": "2025-12-31T11:24:41.317+01:00",
      "additionalData": {}
      
      "raw": "{ ...}",
    }
  ],
  "isDetected": true
}


## Plugin: Fibratus

```
<Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
  <System>
    <Provider Name="Fibratus" />
    <EventID Qualifiers="8192">62873</EventID>
    <Version>0</Version>
    <Level>4</Level>
    <Task>0</Task>
    <Opcode>0</Opcode>
    <Keywords>0x80000000000000</Keywords>
    <TimeCreated SystemTime="2026-01-14T04:56:57.8822028Z" />
    <EventRecordID>134</EventRecordID>
    <Correlation />
    <Execution ProcessID="6224" ThreadID="0" />
    <Channel>Application</Channel>
    <Computer>DESKTOP-C0HF6MF</Computer>
    <Security />
  </System>
  <EventData>
    <Data>Credential discovery via VaultCmd tool

Severity: medium

System event involved in this alert:

	Event #1:

		Seq: 2043790
		Pid: 3216
		Tid: 3752
		Name: CreateProcess
		Category: process
		Host: DESKTOP-C0HF6MF
		Timestamp: 2026-01-14 05:56:44.2745223 +0100 CET
		Parameters: cmdline➜ VaultCmd.exe  /listcreds:"Windows Credentials" /all, directory_table_base➜ 12e078000, domain➜ DESKTOP-C0HF6MF, exe➜ VaultCmd.exe, exit_status➜ Success, flags➜ , kproc➜ ffffb80e0078a080, name➜ VaultCmd.exe, pid➜ 6132, ppid➜ 3216, real_ppid➜ 3216, session_id➜ 2, sid➜ S-1-5-21-937184543-179303868-2836477951-1001, start_time➜ 2026-01-14 05:56:44.2745223 +0100 CET, username➜ hacker
    
		Pid:  3216
		Ppid: 7120
		Name: cmd.exe
		Cmdline: "C:\WINDOWS\system32\cmd.exe" 
		Exe:  C:\Windows\System32\cmd.exe
		Cwd:  C:\Windows\System32\
		SID:  S-1-5-21-937184543-179303868-2836477951-1001
		Username: hacker
		Domain: DESKTOP-C0HF6MF
		Args: []
		Session ID: 2
		Ancestors: 
	
</Data>
  </EventData>
</Event>
```

```
<Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
  <System>
    <Provider Name="Fibratus" />
    <EventID Qualifiers="8192">63003</EventID>
    <Version>0</Version>
    <Level>4</Level>
    <Task>0</Task>
    <Opcode>0</Opcode>
    <Keywords>0x80000000000000</Keywords>
    <TimeCreated SystemTime="2026-01-14T04:54:51.7879635Z" />
    <EventRecordID>128</EventRecordID>
    <Correlation />
    <Execution ProcessID="6224" ThreadID="0" />
    <Channel>Application</Channel>
    <Computer>DESKTOP-C0HF6MF</Computer>
    <Security />
  </System>
  <EventData>
    <Data>Suspicious access to the hosts file

Suspicious process C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe accessed the hosts file for potential tampering


Severity: medium

System events involved in this alert:

	Event #1:

		Seq: 1748233
		Pid: 4324
		Tid: 10840
		Name: CreateProcess
		Category: process
		Host: DESKTOP-C0HF6MF
		Timestamp: 2026-01-14 05:52:58.8910719 +0100 CET
		Parameters: cmdline➜ "C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe" --type=utility --utility-sub-type=network.mojom.NetworkService --lang=en-US --service-sandbox-type=none --noerrdialogs --user-data-dir="C:\Users\hacker\AppData\Local\Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView" --webview-exe-name=Widgets.exe --webview-exe-version=424.1301.2770.0 --embedded-browser-webview=1 --no-appcompat-clear --mojo-platform-channel-handle=3048 --field-trial-handle=2312,i,10267031729652099757,6515291680585498514,262144 --enable-features=MojoIpcz,UseBackgroundNativeThreadPool,UseNativeThreadPool,msWebView2TreatAppSuspendAsDeviceSuspend --variations-seed-version /prefetch:3 /pfhostedapp:fba268c25307ce91690b14c38c05f398edcec8c5MicrosoftWindows.Client.WebExperience_424.1301.270.9_x64__cw5n1h2txyewyWidge, directory_table_base➜ 530c7000, domain➜ DESKTOP-C0HF6MF, exe➜ C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe, exit_status➜ Success, flags➜ APPLICATION_ID|PACKAGED, kproc➜ ffffb80e05293080, name➜ msedgewebview2.exe, pid➜ 9076, ppid➜ 4324, real_ppid➜ 4324, session_id➜ 2, sid➜ S-1-5-21-937184543-179303868-2836477951-1001, start_time➜ 2026-01-14 05:52:58.8910344 +0100 CET, username➜ hacker
    
		Pid:  4324
		Ppid: 7292
		Name: msedgewebview2.exe
		Cmdline: "C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe" --embedded-browser-webview=1 --webview-exe-name=Widgets.exe --webview-exe-version=424.1301.2770.0 --user-data-dir="C:\Users\hacker\AppData\Local\Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView" --noerrdialogs --disk-cache-size=52428800 --edge-webview-is-background --enable-features=MojoIpcz,msWebView2TreatAppSuspendAsDeviceSuspend,UseNativeThreadPool,UseBackgroundNativeThreadPool --lang=en-US --accept-lang=en-US --mojo-named-platform-channel-pipe=7292.6404.13169143304204931738 /pfhostedapp:fba268c25307ce91690b14c38c05f398edcec8c5
		Exe:  C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe
		Cwd:  C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\
		SID:  S-1-5-21-937184543-179303868-2836477951-1001
		Username: hacker
		Domain: DESKTOP-C0HF6MF
		Args: []
		Session ID: 2
		Ancestors: 
	
	Event #2:

		Seq: 1752844
		Pid: 9076
		Tid: 10720
		Name: CreateFile
		Category: file
		Host: DESKTOP-C0HF6MF
		Timestamp: 2026-01-14 05:52:58.9910983 +0100 CET
		Parameters: attributes➜ , create_disposition➜ OPEN, create_options➜ OPEN_REPARSE_POINT, file_object➜ ffffb80e091431a0, file_path➜ C:\WINDOWS\system32\drivers\etc\hosts, irp➜ ffffb80e02ba60f8, share_mask➜ READ|WRITE|DELETE, status➜ Success, tid➜ 10720, type➜ Directory
    
		Pid:  9076
		Ppid: 4324
		Name: msedgewebview2.exe
		Parent name: msedgewebview2.exe
		Cmdline: "C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe" --type=utility --utility-sub-type=network.mojom.NetworkService --lang=en-US --service-sandbox-type=none --noerrdialogs --user-data-dir="C:\Users\hacker\AppData\Local\Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView" --webview-exe-name=Widgets.exe --webview-exe-version=424.1301.2770.0 --embedded-browser-webview=1 --no-appcompat-clear --mojo-platform-channel-handle=3048 --field-trial-handle=2312,i,10267031729652099757,6515291680585498514,262144 --enable-features=MojoIpcz,UseBackgroundNativeThreadPool,UseNativeThreadPool,msWebView2TreatAppSuspendAsDeviceSuspend --variations-seed-version /prefetch:3 /pfhostedapp:fba268c25307ce91690b14c38c05f398edcec8c5MicrosoftWindows.Client.WebExperience_424.1301.270.9_x64__cw5n1h2txyewyWidge
		Parent cmdline: "C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe" --embedded-browser-webview=1 --webview-exe-name=Widgets.exe --webview-exe-version=424.1301.2770.0 --user-data-dir="C:\Users\hacker\AppData\Local\Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView" --noerrdialogs --disk-cache-size=52428800 --edge-webview-is-background --enable-features=MojoIpcz,msWebView2TreatAppSuspendAsDeviceSuspend,UseNativeThreadPool,UseBackgroundNativeThreadPool --lang=en-US --accept-lang=en-US --mojo-named-platform-channel-pipe=7292.6404.13169143304204931738 /pfhostedapp:fba268c25307ce91690b14c38c05f398edcec8c5
		Exe:  C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe
		Cwd:  C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\
		SID:  S-1-5-21-937184543-179303868-2836477951-1001
		Username: hacker
		Domain: DESKTOP-C0HF6MF
		Args: ["C:\Program Files (x86)\Microsoft\EdgeWebView\Application\122.0.2365.106\msedgewebview2.exe" --type=utility --utility-sub-type=network.mojom.NetworkService --lang=en-US --service-sandbox-type=none --noerrdialogs --user-data-dir="C:\Users\hacker\AppData\Local\Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView" --webview-exe-name=Widgets.exe --webview-exe-version=424.1301.2770.0 --embedded-browser-webview=1 --no-appcompat-clear --mojo-platform-channel-handle=3048 --field-trial-handle=2312,i,10267031729652099757,6515291680585498514,262144 --enable-features=MojoIpcz,UseBackgroundNativeThreadPool,UseNativeThreadPool,msWebView2TreatAppSuspendAsDeviceSuspend --variations-seed-version /prefetch:3 /pfhostedapp:fba268c25307ce91690b14c38c05f398edcec8c5MicrosoftWindows.Client.WebExperience_424.1301.270.9_x64__cw5n1h2txyewyWidge]
		Session ID: 2
		Ancestors: msedgewebview2.exe (4324)
	
</Data>
  </EventData>
</Event>
```
