REM %1 = Solution Directory
REM %2 = $(ConfigurationName) Debug/Release

rem copy commandline utility
copy %1CameraControlCmd\Bin\%2\CameraControlCmd.exe %1CameraControl\Bin\%2\ /Y

rem Copy additional Files
xcopy %1CameraControl.Application %1CameraControl\Bin\%2\ /Y /E /R /I /D
xcopy %1CameraControl.Application\*.* %1CameraControl\Bin\%2\ /Y
rem Copy additional Files
MD %1CameraControl\Bin\%2\Plugins
rem copy %1CameraControl.Plugins\Bin\%2\CameraControl.Plugins.* %1CameraControl\Bin\%2\Plugins\ /Y 

copy %1MtpTester\Bin\%2\MtpTester.exe %1CameraControl\Bin\%2\ /Y 

