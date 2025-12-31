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

