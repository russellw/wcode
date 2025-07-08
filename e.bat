echo I just ran `dotnet run --project wcode.Cli example/instructions.txt example` and directed output to log.txt. Look in example/ and log.txt for the results |clip
dotnet run --project wcode.Cli example/instructions.txt example|tee log.txt
