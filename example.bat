﻿@echo off
:s
PingTool.exe -host=8.8.8.8 -host=8.8.4.4 -sourceHost=213.100.249.192
netsh mbn disconnect interface="Mobile Broadband Connection"
netsh mbn connect interface="Mobile Broadband Connection" connmode=name name="Your Service Provider Name"
sleep 15
goto s