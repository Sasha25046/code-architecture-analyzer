namespace CodeArchitectureAnalyzer.Tests;

/// <summary>Вихідний код прикладів для тестів. Усі типи називаються Foo.</summary>
internal static class Samples
{
    // ---------- Позитивні приклади ----------

    public const string ClassicProperty = """
public class Foo
{
    private static Foo _instance;

    private Foo() { }

    public static Foo Instance => _instance ??= new Foo();
}
""";

    public const string StaticMethod = """
public class Foo
{
    private static Foo _instance;

    private Foo() { }

    public static Foo GetInstance()
    {
        if (_instance == null)
        {
            _instance = new Foo();
        }
        return _instance;
    }
}
""";

    public const string LazyProperty = """
using System;

public class Foo
{
    private static readonly Lazy<Foo> _lazy = new Lazy<Foo>(() => new Foo());

    private Foo() { }

    public static Foo Instance => _lazy.Value;
}
""";

    public const string Locked = """
public class Foo
{
    private static readonly object _lock = new object();
    private static volatile Foo _instance;

    private Foo() { }

    public static Foo Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Foo();
                    }
                }
            }
            return _instance;
        }
    }
}
""";

    public const string AutoProperty = """
public class Foo
{
    private Foo() { }

    public static Foo Instance { get; } = new Foo();
}
""";

    public const string PublicField = """
public class Foo
{
    private Foo() { }

    public static readonly Foo Instance = new Foo();
}
""";

    public const string NullableAnnotated = """
#nullable enable
public class Foo
{
    private static Foo? _instance;

    private Foo() { }

    public static Foo GetInstance() => _instance ??= new Foo();
}
""";

    public const string PartialFirst = """
public partial class Foo
{
    private static Foo _instance;

    private Foo() { }
}
""";

    public const string PartialSecond = """
public partial class Foo
{
    public static Foo Instance => _instance ??= new Foo();
}
""";

    // ---------- Негативні приклади ----------

    public const string PublicConstructor = """
public class Foo
{
    private static Foo _instance;

    public Foo() { }

    public static Foo Instance => _instance ??= new Foo();
}
""";

    public const string StaticClass = """
public static class Foo
{
    public static int Add(int a, int b) => a + b;
}
""";

    public const string ImplicitConstructor = """
public class Foo
{
    private static Foo _instance = new Foo();

    public static Foo Instance => _instance;
}
""";

    public const string PrivateProtectedConstructor = """
public class Foo
{
    private static Foo _instance;

    private protected Foo() { }

    public static Foo Instance => _instance ??= new Foo();
}
""";

    public const string ProtectedConstructor = """
public class Foo
{
    private static Foo _instance;

    protected Foo() { }

    public static Foo Instance => _instance ??= new Foo();
}
""";

    public const string NoPublicAccessor = """
public class Foo
{
    private static Foo _instance;

    private Foo() { }

    internal static Foo Get() => _instance ??= new Foo();
}
""";

    public const string FactoryWithoutStorage = """
public class Foo
{
    private static Foo _unused;

    private Foo() { }

    public static Foo Create() => new Foo();
}
""";

    public const string NoStaticStorage = """
public class Foo
{
    private Foo() { }

    public static Foo Create() => new Foo();
}
""";

    public const string SameNameInOtherNamespace = """
namespace One
{
    public class Foo
    {
        private static Two.Foo _other;

        private Foo() { }

        public static Two.Foo Get() => _other;
    }
}

namespace Two
{
    public class Foo
    {
        public Foo() { }
    }
}
""";
}
