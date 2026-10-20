namespace CodeArchitectureAnalyzer.Cli;

internal static class SampleCode
{
    public const string Text = """
namespace SampleProject;

// Еталонний Singleton
public class DatabaseConnection
{
    private static DatabaseConnection _instance;

    private DatabaseConnection() { }

    public static DatabaseConnection Instance => _instance ??= new DatabaseConnection();
}

// Singleton на основі Lazy<T>
public class Logger
{
    private static readonly System.Lazy<Logger> _lazy = new(() => new Logger());

    private Logger() { }

    public static Logger Instance => _lazy.Value;
}

// Звичайний сервісний клас (не Singleton)
public class UserService
{
    public UserService() { }
    public void CreateUser() { }
}

// Статичний клас (не Singleton)
public static class MathHelper
{
    public static int Add(int a, int b) => a + b;
}

// Закритий конструктор, але щоразу новий об'єкт (не Singleton)
public class ReportBuilder
{
    private static ReportBuilder _unused;

    private ReportBuilder() { }

    public static ReportBuilder Create() => new ReportBuilder();
}
""";
}
