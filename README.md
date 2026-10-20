
bash setup.sh
dotnet run --project src/CodeArchitectureAnalyzer.Cli                      
dotnet run --project src/CodeArchitectureAnalyzer.Cli -- <шлях> --md report.md  
dotnet run --project src/CodeArchitectureAnalyzer.Cli -- <шлях> --all      
dotnet test
