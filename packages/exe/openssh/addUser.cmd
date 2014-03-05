@echo off
cygpath.exe -H > pathtmp.txt
for /F %%I in (pathtmp.txt) DO set cyghometmp=%%I
set userarg=%2
IF defined userarg (
mkpasswd %1 -u %userarg% -p%cyghometmp% >> ../etc/passwd
) ELSE (
mkpasswd %1 -p%cyghometmp% >> ../etc/passwd
)
set cyghometmp=
set userarg=
del pathtmp.txt