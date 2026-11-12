Аналізатор шаблонів проєктування (прототип)

Статичний аналізатор C#-коду на основі Roslyn. Детектори:

- SingletonDetector — шаблон Одинак (умови C0–C4)
- FactoryMethodDetector — Фабричний метод (умови F1–F4)
- BrainMethodDetector — антипатерн Метод-мозок (умови B1–B4, пороги CC/LOC/MaxNestingDepth/NOAV)
- MetricsCalculator — метрики методів (LOC, CC, MaxNestingDepth, NOAV)

Структура

- src/CodeArchitectureAnalyzer.Core — бібліотека: компіляція, детектори, метрики, модель результатів, звіт Markdown
- src/CodeArchitectureAnalyzer.Cli — консольний застосунок
- tests/CodeArchitectureAnalyzer.Tests — модульні тести xUnit

Запуск


dotnet build
dotnet test

dotnet run --project src/CodeArchitectureAnalyzer.Cli                              # вбудований приклад
dotnet run --project src/CodeArchitectureAnalyzer.Cli -- <шлях до файлу/каталогу>  # результати в консолі
dotnet run --project src/CodeArchitectureAnalyzer.Cli -- <шлях> --all              # разом із негативними
dotnet run --project src/CodeArchitectureAnalyzer.Cli -- <шлях> --md report.md     # зберегти звіт у Markdown

