@echo off
:s
PingTool.exe -host=8.8.8.8 -host=8.8.4.4 -sourceHost=192.168.0.3
netsh mbn disconnect interface="Mobile Broadband Connection"
netsh mbn connect interface="Mobile Broadband Connection" connmode=name name="Your Service Provider Name"
ping -n 16 127.0.0.1
REM sleep 15
goto s
