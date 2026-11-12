namespace CodeArchitectureAnalyzer.Tests;

/// <summary>Приклади для тестів FactoryMethodDetector.</summary>
internal static class FactoryMethodSamples
{
    // ---------- Позитивні ----------

    public const string FormAAbstract = """
public abstract class Product { }
public class ConcreteA : Product { }
public class ConcreteB : Product { }

public abstract class Creator
{
    public abstract Product Create();
}

public class CreatorA : Creator
{
    public override Product Create() => new ConcreteA();
}

public class CreatorB : Creator
{
    public override Product Create()
    {
        return new ConcreteB();
    }
}
""";

    public const string FormBInterface = """
public interface IProduct { }
public class ProductA : IProduct { }
public class ProductB : IProduct { }

public interface IFactory
{
    IProduct Create();
}

public class FactoryA : IFactory
{
    public IProduct Create() => new ProductA();
}

public class FactoryB : IFactory
{
    public IProduct Create()
    {
        return new ProductB();
    }
}
""";

    public const string VirtualWithLocalVariable = """
public interface IProduct { }
public class ProductA : IProduct { }

public class Creator
{
    public virtual IProduct Create() => new ProductA();
}

public class SpecialCreator : Creator
{
    public override IProduct Create()
    {
        var product = new ProductA();
        return product;
    }
}
""";

    public const string TargetTypedNew = """
public abstract class Product { }
public class ConcreteA : Product { }

public abstract class Creator
{
    public abstract Product Create();
}

public class CreatorA : Creator
{
    public override Product Create()
    {
        ConcreteA product = new();
        return product;
    }
}
""";

    // ---------- Негативні ----------

    public const string StoredObject = """
public abstract class Product { }
public class ConcreteA : Product { }

public abstract class Creator
{
    public abstract Product Create();
}

public class CreatorA : Creator
{
    private readonly Product _cached = new ConcreteA();

    public override Product Create() => _cached;
}
""";

    public const string IndirectCreation = """
public abstract class Product { }
public class ConcreteA : Product { }

public abstract class Creator
{
    public abstract Product Create();
}

public class CreatorA : Creator
{
    public override Product Create() => Build();

    private static Product Build() => new ConcreteA();
}
""";

    public const string NoImplementations = """
public abstract class Product { }

public abstract class Creator
{
    public abstract Product Create();
}
""";

    public const string NonPolymorphic = """
public interface IProduct { }
public class ProductA : IProduct { }

public class SimpleCreator
{
    public IProduct Create() => new ProductA();
}
""";

    public const string StaticFactory = """
public interface IProduct { }
public class ProductA : IProduct { }

public class Factory
{
    public static IProduct Create() => new ProductA();
}
""";
}
