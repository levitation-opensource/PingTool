## Ping Monitor
A program to monitor a number of hosts using ping. The program exits when all monitored hosts have an outage. The program allows specifying the ping source interface by IP address. For Windows.

## Use cases
If there is some network connection that occasionally misbehaves and loses reliable connection then you can use Ping Monitor tool to launch corrective actions upon detection of the occurrence of the problem. 

You can create a batch file which contains a loop and inside that loop two sets of commands: the first command starts the Ping Monitor tool. If the first command quits then that means that the trigger situation has been detected and therefore it is appropriate time for the second set of commands to be executed. The second set of commands would contain some corrective action. For example, the second set of commands could:
<br>&nbsp;&nbsp;&nbsp;&nbsp;a) Disconnect and reconnect the misbehaving network connection.
<br>&nbsp;&nbsp;&nbsp;&nbsp;b) Switch to an alternate wifi network.
<br>&nbsp;&nbsp;&nbsp;&nbsp;c) Trigger a power cycle of the router when the router is powered through a USB controlled programmable power strip.

Example batch file content:

	@echo off
	:s
	PingTool.exe -host=8.8.8.8 -host=8.8.4.4 -sourceHost=192.168.0.3
	netsh mbn disconnect interface="Mobile Broadband Connection"
	netsh mbn connect interface="Mobile Broadband Connection" connmode=name name="Your Service Provider Name"
	ping -n 16 127.0.0.1
	REM sleep 15
	goto s

The above example pings two IP addresses belonging to Google. The pings are performed via the network interface having a local address of 192.168.0.3. Once pings to ALL of the specified target IP addresses start failing, the network connection is restarted by the following commands in the batch file. By default the trigger activates (that is, Ping Tool quits) when ALL of the target IP addresses fail ping for 3 consequtive checks with 5 second intervals, and then the outage continues for another 30 seconds after that. If the monitored IP addresses ping successfully during that additional time interval then the trigger is reset and Ping Tool continues running without quitting.

### State
Ready to use. Maintained and in active use.

### Program arguments and their default values
<br>-help (Shows help text)
<br>-outageTimeBeforeGiveUpSeconds=180 (How long outage should last before trigger is activated and PingTool quits. NB! This timeout starts only after the failure count specified with -outageConditionNumPings has been exceeded.)
<br>-outageConditionNumPings=3 (How many pings should fail before outage can be declared)
<br>-passedPingIntervalMs=15000 (How many ms to pause after a successful ping)
<br>-failedPingIntervalMs=5000 (How many ms to pause after a failed ping)
<br>-pingTimeoutMs=10000 (Ping timeout)
<br>-host=host name (Host name. Multiple host names can be specified. In this case the program exits only after ALL monitored hosts have failed.)
<br>-sourceHost=source host name (Source host name. Multiple source host names can be specified. If multiple names are specified then each target host is pinged through corresponding source host (interface). If no source host is specified for some target host then the ping path is automatically choosen by the operating system.)


[![Analytics](https://ga-beacon.appspot.com/UA-351728-28/PingTool/README.md?pixel)](https://github.com/igrigorik/ga-beacon)    
