If Not Exist "Debug\pgdata" "3rdParty\pgsql\9.6.1\bin\initdb.exe" -D "Debug\pgdata" -U postgres -E UTF8 --no-locale
"3rdParty\pgsql\9.6.1\bin\pg_ctl" -D "Debug\pgdata" stop
"3rdParty\pgsql\9.6.1\bin\pg_ctl" -D "Debug\pgdata" start
