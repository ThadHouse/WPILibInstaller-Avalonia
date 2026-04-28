@echo off

pushd "%~dp0..\jdk"
set JAVA_HOME=%CD%
set PATH=%CD%\bin;%PATH%
popd
